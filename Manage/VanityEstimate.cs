using System;
using System.Linq;

namespace jmsTools.Manage
{
    internal static class VanityEstimate
    {
        public static double ExpectedTemplateSet(bool sol, bool matchCase, VanityTemplate[] templates)
        {
            if (!sol)
                return templates.Length / templates.Sum(item => 1 / ExpectedAttempts(false, matchCase, "", "", item));

            // Skip subsets such as 9 identical characters when 8 already qualify.
            var distinct = new System.Collections.Generic.List<VanityTemplate>();
            foreach (var item in templates.OrderBy(value => value.Length))
                if (!distinct.Any(other => IsSubset(item, other))) distinct.Add(item);
            return 1 / Math.Min(1, distinct.Sum(item => 1 / ExpectedAttempts(true, matchCase, "", "", item)));
        }

        private static bool IsSubset(VanityTemplate longer, VanityTemplate shorter)
        {
            if (longer.AtEnd != shorter.AtEnd || longer.Length < shorter.Length) return false;
            if (longer.Kind == 2 || longer.Kind == 3 || shorter.Kind == 2 || shorter.Kind == 3) return longer.Kind == shorter.Kind;
            int offset = longer.AtEnd ? longer.Length - shorter.Length : 0;
            for (int i = 1; i < shorter.Length; i++)
                for (int j = 0; j < i; j++)
                    if ((shorter.Pattern[i] == shorter.Pattern[j]) != (longer.Pattern[offset + i] == longer.Pattern[offset + j])) return false;
            return true;
        }
        public static double ExpectedAttempts(bool sol, bool matchCase, string prefix, string suffix, VanityTemplate template)
        {
            if (template == null)
            {
                string pattern = prefix + suffix;
                return sol ? SolanaVanityGenerator.EstimateAttempts(pattern, matchCase)
                    : Math.Pow(16, Math.Max(1, pattern.Length)) * (matchCase ? Math.Pow(2, LetterCount(pattern)) : 1);
            }

            bool sequence = template.Kind == 2 || template.Kind == 3;
            int[] groups = template.Pattern.GroupBy(symbol => symbol).Select(group => group.Count()).ToArray();
            if (!sol)
            {
                // CUDA searches one concrete expansion per wallet. Average its work,
                // including checksum casing, over possible expansions.
                double casing = !matchCase ? 1 : sequence
                    ? template.SequenceVariants(false).Average(value => Math.Pow(2, LetterCount(value)))
                    : WeightedAssignments(10, 6, groups) / WeightedAssignments(16, 0, groups);
                return Math.Pow(16, template.Length) * casing;
            }

            // Approximate Base58 positions as independent; SOL head characters are biased.
            var weights = SolanaVanityGenerator.Alphabet
                .GroupBy(c => matchCase ? c : char.ToLowerInvariant(c))
                .ToDictionary(group => group.Key, group => group.Count());
            double matches;
            if (sequence)
            {
                matches = 0;
                int step = template.Kind == 2 ? 1 : -1;
                foreach (char start in weights.Keys)
                {
                    int ways = 1;
                    for (int i = 0; i < template.Length; i++)
                    {
                        char next = (char)(start + i * step);
                        if (!SameGroup(start, next) || !weights.TryGetValue(next, out int variants)) { ways = 0; break; }
                        ways *= variants;
                    }
                    matches += ways;
                }
            }
            else matches = WeightedAssignments(weights.Count(pair => pair.Value == 1), weights.Count(pair => pair.Value == 2), groups);
            return Math.Pow(58, template.Length) / matches;
        }

        private static int LetterCount(string value) => value.Count(c => char.ToLowerInvariant(c) >= 'a' && char.ToLowerInvariant(c) <= 'f');

        private static bool SameGroup(char a, char b) =>
            a >= '0' && a <= '9' ? b >= '0' && b <= '9' :
            a >= 'A' && a <= 'Z' ? b >= 'A' && b <= 'Z' : b >= 'a' && b <= 'z';

        // Assign distinct symbols from pools with one or two possible character forms.
        private static double WeightedAssignments(int singles, int doubles, int[] repetitions)
        {
            var ways = new double[repetitions.Length + 1]; ways[0] = 1;
            for (int i = 0; i < repetitions.Length; i++)
            {
                double weight = Math.Pow(2, repetitions[i]);
                for (int j = i + 1; j >= 0; j--)
                    ways[j] = ways[j] * Math.Max(0, singles - (i - j)) +
                        (j == 0 ? 0 : ways[j - 1] * Math.Max(0, doubles - (j - 1)) * weight);
            }
            return ways.Sum();
        }

        public static double MedianAttempts(double expected) => expected <= 1 ? 1 :
            Math.Ceiling(expected > 1e8 ? Math.Log(2) * expected : -Math.Log(2) / Math.Log(1 - 1 / expected));
    }
}
