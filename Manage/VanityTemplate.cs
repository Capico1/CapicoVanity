using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace jmsTools.Manage
{
    internal sealed class VanityTemplate
    {
        // Codes are shared with jms_sol_template_matches in the OpenCL kernel.
        public int Kind { get; private set; }
        public int Length { get; private set; }
        public bool AtEnd { get; private set; }
        public string Pattern { get; private set; }

        public static VanityTemplate Parse(string pattern, bool atEnd, bool sol)
        {
            pattern = (pattern ?? "").Trim().ToUpperInvariant();
            int description = pattern.IndexOf('（');
            if (description >= 0) pattern = pattern.Substring(0, description).Trim();
            if (pattern.Length < 3 || pattern.Length > 12 || pattern.Any(c => c < 'A' || c > 'Z'))
                throw new ArgumentException("模板为 3–12 位字母，例如 AAAA、ABCDE、ABAB、AABB、ABBA。");
            int kind;
            if (pattern.All(c => c == pattern[0])) kind = 1;
            else if (Enumerable.Range(1, pattern.Length - 1).All(i => pattern[i] == pattern[i - 1] + 1)) kind = 2;
            else if (Enumerable.Range(1, pattern.Length - 1).All(i => pattern[i] == pattern[i - 1] - 1)) kind = 3;
            else if (pattern == "ABAB") kind = 4;
            else if (pattern == "AABB") kind = 5;
            else if (pattern == "ABBA") kind = 6;
            else if (pattern.Distinct().Count() < pattern.Length) kind = 7;
            else throw new ArgumentException("模板需包含重复字母或连续顺子，例如 AAAA、ABCDE、ABCABC、ABCCBA。");
            var value = new VanityTemplate { Pattern = pattern, Length = pattern.Length, Kind = kind, AtEnd = atEnd };
            if ((kind == 2 || kind == 3) && value.SequenceVariants(sol).Count == 0)
                throw new ArgumentException(sol ? "SOL 中没有符合此长度的连续有效 Base58 字符。" : "EVM 数字顺子最多 10 位，字母顺子最多 6 位，且不跨数字和字母。");
            return value;
        }

        private static int Group(char c) => c >= '0' && c <= '9' ? 1 : c >= 'A' && c <= 'Z' ? 2 : c >= 'a' && c <= 'z' ? 3 : 0;

        public bool Matches(string address, bool sol, bool matchCase)
        {
            if (!sol && address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) address = address.Substring(2);
            if (address.Length < Length) return false;
            string part = address.Substring(AtEnd ? address.Length - Length : 0, Length);
            if (!matchCase) part = part.ToLowerInvariant();
            switch (Kind)
            {
                case 1: return part.All(c => c == part[0]);
                case 2:
                case 3:
                    return Enumerable.Range(1, Length - 1).All(i => Group(part[i]) != 0 && Group(part[i]) == Group(part[i - 1]) && part[i] == part[i - 1] + (Kind == 2 ? 1 : -1));
                case 4: return part[0] != part[1] && part[0] == part[2] && part[1] == part[3];
                case 5: return part[0] != part[2] && part[0] == part[1] && part[2] == part[3];
                case 6: return part[0] != part[1] && part[0] == part[3] && part[1] == part[2];
                case 7:
                    for (int i = 1; i < Length; i++)
                        for (int j = 0; j < i; j++)
                            if ((Pattern[i] == Pattern[j]) != (part[i] == part[j])) return false;
                    return true;
                default: return false;
            }
        }

        public int RepeatLength(string address, bool sol, bool matchCase)
        {
            if (!sol && address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) address = address.Substring(2);
            if (!matchCase) address = address.ToLowerInvariant();
            int count = 1;
            while (count < address.Length && (AtEnd ? address[address.Length - 1 - count] == address[address.Length - 1] : address[count] == address[0])) count++;
            return count;
        }

        internal List<string> SequenceVariants(bool sol)
        {
            var values = new List<string>();
            foreach (string group in sol ? new[] { "123456789", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "abcdefghijklmnopqrstuvwxyz" } : new[] { "0123456789", "abcdef" })
                for (int start = 0; start + Length <= group.Length; start++)
                {
                    string value = group.Substring(start, Length);
                    if (sol && value.Any(c => SolanaVanityGenerator.Alphabet.IndexOf(c) < 0)) continue;
                    values.Add(Kind == 3 ? new string(value.Reverse().ToArray()) : value);
                }
            return values;
        }

        private static int Next(RandomNumberGenerator random, int maximum)
        {
            var bytes = new byte[1];
            int limit = 256 - 256 % maximum;
            do { random.GetBytes(bytes); } while (bytes[0] >= limit);
            return bytes[0] % maximum;
        }

        // The EVM backend accepts concrete positional patterns. Select a valid
        // expansion on each search; CUDA still performs the actual address search.
        public string CreateEvmLiteral()
        {
            using (var random = RandomNumberGenerator.Create())
            {
                if (Kind == 2 || Kind == 3)
                {
                    var choices = SequenceVariants(false);
                    return choices[Next(random, choices.Count)];
                }
                const string alphabet = "0123456789abcdef";
                if (Kind == 7)
                {
                    var available = alphabet.ToList();
                    var mapping = new Dictionary<char, char>();
                    return new string(Pattern.Select(symbol => {
                        if (!mapping.ContainsKey(symbol))
                        {
                            int index = Next(random, available.Count);
                            mapping[symbol] = available[index]; available.RemoveAt(index);
                        }
                        return mapping[symbol];
                    }).ToArray());
                }
                int first = Next(random, 16), second = (first + 1 + Next(random, 15)) % 16;
                char a = alphabet[first], b = alphabet[second];
                if (Kind == 1) return new string(a, Length);
                if (Kind == 4) return new string(new[] { a, b, a, b });
                if (Kind == 5) return new string(new[] { a, a, b, b });
                return new string(new[] { a, b, b, a });
            }
        }

        public string Example(bool sol)
        {
            if (Kind == 1) return new string('7', Length);
            if (Kind == 2 || Kind == 3) return SequenceVariants(sol).First();
            if (Kind == 4) return "7b7b";
            if (Kind == 5) return "77bb";
            if (Kind == 7)
            {
                const string examples = "7b3d5f9a2c4e6810";
                var symbols = Pattern.Distinct().ToList();
                return new string(Pattern.Select(symbol => examples[symbols.IndexOf(symbol)]).ToArray());
            }
            return "7bb7";
        }
    }
}
