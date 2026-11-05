using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.DataEncoders;
using Solana.Unity.Wallet;

namespace jmsTools.Manage
{
    internal static class SolanaVanityGenerator
    {
        internal const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        // Base58 prefixes are not uniformly distributed; this is a rough estimate.
        public static double EstimateAttempts(string pattern, bool matchCase)
        {
            double attempts = 1;
            foreach (char c in pattern)
            {
                int variants = matchCase ? 1 : Alphabet.Count(a => char.ToUpperInvariant(a) == char.ToUpperInvariant(c));
                attempts *= 58.0 / Math.Max(1, variants);
            }
            return attempts;
        }

        public static string ValidatePattern(string prefix, string suffix, bool matchCase)
        {
            if (prefix.Length + suffix.Length > 44) return "SOL 前后缀合计不能超过 44 位。";
            foreach (char c in prefix + suffix)
            {
                bool valid = Alphabet.IndexOf(c) >= 0 || (!matchCase && Alphabet.Any(
                    a => char.ToUpperInvariant(a) == char.ToUpperInvariant(c)));
                if (!valid) return "SOL 使用 Base58 字符，不支持 0、O、I、l（忽略大小写时可匹配对应的有效字母）。";
            }
            return null;
        }

        public static bool Matches(string address, string prefix, string suffix, bool matchCase)
        {
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return address.StartsWith(prefix, comparison) && address.EndsWith(suffix, comparison);
        }

        public static void VerifyResult(VanityGpuGenerator.Result result)
        {
            var secret = Encoders.Base58.DecodeData(result.PrivateKey);
            if (secret.Length != 64) throw new InvalidOperationException("SOL 私钥必须是 64 字节密钥对。");
            var seed = secret.Take(32).ToArray();
            var publicKey = Chaos.NaCl.Ed25519.PublicKeyFromSeed(seed);
            var privateKey = Chaos.NaCl.Ed25519.ExpandedPrivateKeyFromSeed(seed);
            // Re-derive from the seed, rather than trusting the public bytes embedded in the keypair.
            if (Encoders.Base58.EncodeData(publicKey) != result.Address || !privateKey.SequenceEqual(secret))
                throw new InvalidOperationException("SOL 私钥与地址校验失败。");
            var restored = new Account(privateKey, publicKey);
            var message = Encoding.UTF8.GetBytes("JmsTools local SOL vanity verification");
            if (!restored.PublicKey.Verify(message, restored.Sign(message)))
                throw new InvalidOperationException("SOL 钱包签名校验失败。");
        }

        public static VanityGpuGenerator.Result Generate(string prefix, string suffix, bool matchCase,
            int workers, CancellationToken token, Action onAttempt)
        {
            string error = ValidatePattern(prefix, suffix, matchCase);
            if (error != null) throw new ArgumentException(error);
            VanityGpuGenerator.Result result = null;
            var resultLock = new object();
            Parallel.For(0, workers, new ParallelOptions { CancellationToken = token }, (index, loop) => {
                while (!token.IsCancellationRequested && !loop.IsStopped)
                {
                    var account = new Account();
                    string address = account.PublicKey.Key;
                    onAttempt();
                    if (!Matches(address, prefix, suffix, matchCase)) continue;
                    lock (resultLock)
                    {
                        if (result == null)
                        {
                            var candidate = new VanityGpuGenerator.Result { Address = address, PrivateKey = account.PrivateKey.Key };
                            VerifyResult(candidate);
                            result = candidate;
                        }
                    }
                    loop.Stop();
                }
            });
            token.ThrowIfCancellationRequested();
            return result;
        }
    }
}
