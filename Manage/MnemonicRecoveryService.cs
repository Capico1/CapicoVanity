using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.DataEncoders;
using Nethereum.HdWallet;

namespace jmsTools.Manage
{
    public sealed class MnemonicRecoveryRequest
    {
        public string Network { get; set; }
        public string TargetAddress { get; set; }
        public string[] PhraseMask { get; set; }
        public string[] CandidateWords { get; set; }
        public string Passphrase { get; set; }
        public int AddressIndex { get; set; }
        public bool AllowRepeatedWords { get; set; }
        public bool UseGpu { get; set; } = true;
        public int GpuBatchSize { get; set; } = 0;
        public int Threads { get; set; } = Math.Max(1, Environment.ProcessorCount);

        public void Validate()
        {
            if (Network != "ETH" && Network != "BSC" && Network != "SOL")
                throw new ArgumentException("恢复网络必须是 ETH、BSC 或 SOL。");
            ValidateTargetAddress(Network, TargetAddress);
            if (PhraseMask == null || PhraseMask.Length != 12)
                throw new ArgumentException("助记词模板必须正好包含 12 个位置。");
            if (CandidateWords == null) CandidateWords = new string[0];
            var candidates = CandidateWords.Select(value => (value ?? "").Trim().ToLowerInvariant())
                .Where(value => value.Length > 0).Distinct().ToArray();
            if (PhraseMask.Any(IsUnknownWord)
                && candidates.Length == 0)
                throw new ArgumentException("模板含空位时，请选择至少包含一个 BIP39 词的候选词表。");
            foreach (var word in PhraseMask.Where(value => !IsUnknownWord(value)))
                if (!Wordlist.English.WordExists(word, out _))
                    throw new ArgumentException("模板包含非英文 BIP39 词，请检查输入。");
            foreach (var word in candidates)
                if (!Wordlist.English.WordExists(word, out _))
                    throw new ArgumentException("候选词表含非英文 BIP39 词：" + word);
            if (AddressIndex < 0 || AddressIndex > 999999)
                throw new ArgumentException("地址索引需在 0 至 999999 之间。");
            if (GpuBatchSize < 0 || GpuBatchSize > 1048576)
                throw new ArgumentException("GPU 每批数量需为自动（0）或 1 至 1,048,576。");
            if (Threads < 1 || Threads > 128)
                throw new ArgumentException("CPU 线程数需在 1 至 128 之间。");
            CandidateWords = candidates;
        }

        private static bool IsUnknownWord(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value == "?" || value == "_";
        }

        public static void ValidateTargetAddress(string network, string address)
        {
            address = (address ?? "").Trim();
            if (network == "SOL")
            {
                if (address.Length < 32 || address.Length > 44)
                    throw new ArgumentException("SOL 地址格式不正确。");
                try
                {
                    if (Encoders.Base58.DecodeData(address).Length != 32)
                        throw new ArgumentException("SOL 地址解码后不是 32 字节。");
                }
                catch (FormatException)
                {
                    throw new ArgumentException("SOL 地址不是有效 Base58 文本。");
                }
            }
            else if (!Regex.IsMatch(address, @"^0x[0-9a-fA-F]{40}$"))
                throw new ArgumentException("ETH / BSC 地址格式不正确，应为 0x 开头的 40 位十六进制地址。");
        }
    }

    public sealed class MnemonicRecoveryProgress
    {
        public long CombinationsChecked { get; set; }
        public long ChecksumValid { get; set; }
        public double CombinationsPerSecond { get; set; }
        public int GpuBatchSize { get; set; }
        public string Engine { get; set; }
    }

    public sealed class MnemonicRecoveryResult
    {
        public string Network { get; set; }
        public string Address { get; set; }
        public string PrivateKey { get; set; }
        public string Mnemonic { get; set; }
        public string Engine { get; set; }
        public int AddressIndex { get; set; }
        public long CombinationsChecked { get; set; }
    }

    public static class MnemonicRecoveryService
    {
        public static async Task<MnemonicRecoveryResult> SearchAsync(
            MnemonicRecoveryRequest request,
            IProgress<MnemonicRecoveryProgress> progress,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");
            request.Validate();
            return await Task.Run(() => Search(request, progress, cancellationToken), cancellationToken);
        }

        public static string EstimateSearchSpace(
            string[] phraseMask,
            string[] candidateWords,
            bool allowRepeatedWords)
        {
            BigInteger total = CalculateSearchSpace(
                phraseMask, candidateWords, allowRepeatedWords);
            return FormatScientific(total);
        }

        private static BigInteger CalculateSearchSpace(
            string[] phraseMask,
            string[] candidateWords,
            bool allowRepeatedWords)
        {
            int unknown = phraseMask == null ? 0 : phraseMask.Count(value => IsUnknown(value));
            int candidates = candidateWords == null ? 0 : candidateWords.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (unknown == 0) return BigInteger.One;
            if (candidates == 0) return BigInteger.Zero;
            BigInteger total;
            if (allowRepeatedWords) total = BigInteger.Pow(candidates, unknown);
            else
            {
                var known = new HashSet<string>(
                    phraseMask == null ? new string[0] : phraseMask
                        .Where(value => !IsUnknown(value))
                        .Select(value => value.Trim().ToLowerInvariant()),
                    StringComparer.OrdinalIgnoreCase);
                int available = candidateWords == null ? 0 : candidateWords
                    .Where(value => !known.Contains(value.Trim().ToLowerInvariant()))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count();
                total = BigInteger.One;
                for (int i = 0; i < unknown; i++)
                {
                    if (available - i <= 0) return BigInteger.Zero;
                    total *= available - i;
                }
            }
            return total;
        }

        // Adjust only the next batch. SOL measures GPU combinations; ETH/BSC
        // measure GPU seed derivation plus the CPU address checks for those seeds.
        private sealed class RecoveryBatch
        {
            public int Current { get; private set; }
            public int Capacity { get; }
            private readonly int minimum;
            private readonly bool automatic;

            public RecoveryBatch(MnemonicRecoveryRequest request)
            {
                automatic = request.GpuBatchSize == 0;
                bool sol = request.Network == "SOL";
                minimum = sol ? 256 : 16;
                int maximum = sol ? 65536 : 2048;
                Capacity = automatic ? (int)BigInteger.Max(1, BigInteger.Min(maximum, CalculateSearchSpace(
                    request.PhraseMask, request.CandidateWords, request.AllowRepeatedWords))) : request.GpuBatchSize;
                Current = automatic ? Math.Min(Capacity, sol ? 4096 : 128) : Capacity;
            }

            public void Observe(int processed, double milliseconds)
            {
                if (!automatic || processed != Current) return;
                if (milliseconds > 300)
                    Current = Math.Max(Math.Min(minimum, Capacity), Current / 2);
                else if (milliseconds < 125)
                    Current = Math.Min(Capacity, Current * 2);
            }
        }

        private static MnemonicRecoveryResult Search(
            MnemonicRecoveryRequest request,
            IProgress<MnemonicRecoveryProgress> progress,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            IntPtr device = IntPtr.Zero;
            string deviceName = null;
            if (request.UseGpu &&
                !(request.Network == "SOL" && request.AllowRepeatedWords) &&
                Encoding.UTF8.GetByteCount("mnemonic" + (request.Passphrase ?? "").Normalize(NormalizationForm.FormKD)) <= 256)
                deviceName = SolanaVanityGpuGenerator.Detect(out device);
            return SearchWithDevice(request, progress, token, device, deviceName);
        }

        private static MnemonicRecoveryResult SearchWithDevice(
            MnemonicRecoveryRequest request, IProgress<MnemonicRecoveryProgress> progress,
            CancellationToken token, IntPtr device, string deviceName)
        {
            string cpuEngine = "CPU × " + request.Threads;
            MnemonicRecoveryResult result = null;
            bool useCpu = device == IntPtr.Zero;
            if (!useCpu)
            {
                try { result = SearchEngine(request, progress, token, device, "GPU · " + deviceName); }
                catch (Exception error) when (VanityGpuGenerator.IsGpuFailure(error))
                {
                    token.ThrowIfCancellationRequested();
                    useCpu = true;
                    cpuEngine += "（GPU 无法运行，已从头重试）";
                }
            }
            else if (request.UseGpu) cpuEngine += "（GPU 不可用或不适用）";

            if (useCpu) result = SearchEngine(request, progress, token, IntPtr.Zero, cpuEngine);
            if (result != null) return result;
            throw new InvalidOperationException("已搜索完全部候选组合，没有找到与该地址匹配的 12 词助记词。");
        }

        private static MnemonicRecoveryResult SearchEngine(
            MnemonicRecoveryRequest request, IProgress<MnemonicRecoveryProgress> progress,
            CancellationToken token, IntPtr device, string engine)
        {
            var watch = Stopwatch.StartNew();
            long checkedCombinations = 0;
            long checksumValid = 0;
            MnemonicRecoveryResult result = null;
            Report(progress, 0, 0, watch, engine);

            if (device != IntPtr.Zero && request.Network != "SOL")
            {
                var batch = new RecoveryBatch(request);
                using (var matcher = new SolanaVanityGpuGenerator.AddressMatcher(
                    device, batch.Capacity, token))
                {
                    result = SearchEvmGpu(request, matcher, batch, engine, progress, watch,
                        ref checkedCombinations, ref checksumValid, token);
                }
            }
            else if (device != IntPtr.Zero)
            {
                var batch = new RecoveryBatch(request);
                using (var matcher = new SolanaVanityGpuGenerator.AddressMatcher(device, batch.Capacity, token))
                {
                    byte[] salt = Bip39Salt(request.Passphrase);
                    var fixedIndices = EnglishIndices(request.PhraseMask, true);
                    var candidateIndices = EnglishIndices(request.CandidateWords, false)
                        .Where(index => !fixedIndices.Contains(index)).ToArray();
                    try
                    {
                    BigInteger totalRank = CalculateSearchSpace(
                        request.PhraseMask, request.CandidateWords, false);
                    if (totalRank.IsZero)
                        throw new ArgumentException("当前模板与候选词表没有可搜索组合。");
                    byte[] wordList = PackEnglishWordList();
                    BigInteger rank = BigInteger.Zero;
                    while (rank < totalRank)
                    {
                        token.ThrowIfCancellationRequested();
                        int count = (int)BigInteger.Min(batch.Current, totalRank - rank);
                        var elapsed = Stopwatch.StartNew();
                        var gpuResult = matcher.MatchGenerated(
                            wordList, fixedIndices, candidateIndices, rank, count,
                            request.TargetAddress.Trim(), salt, token);
                        BigInteger completed = rank + count;
                        checkedCombinations = completed > long.MaxValue
                            ? long.MaxValue : (long)completed;
                        checksumValid = SaturatingAdd(checksumValid, gpuResult.ChecksumValid);
                        batch.Observe(count, elapsed.Elapsed.TotalMilliseconds);
                        Report(progress, checkedCombinations, checksumValid, watch, engine, count);
                        if (gpuResult.Index >= 0)
                        {
                            result = SolanaResult(gpuResult.Phrase, request, engine,
                                checkedCombinations);
                            break;
                        }
                        rank = completed;
                    }
                    }
                    finally
                    {
                        Array.Clear(salt, 0, salt.Length);
                        Array.Clear(fixedIndices, 0, fixedIndices.Length);
                        Array.Clear(candidateIndices, 0, candidateIndices.Length);
                    }
                }
            }
            else
            {
                var phraseSource = EnumeratePhrases(request.PhraseMask, request.CandidateWords,
                    request.AllowRepeatedWords, token);
                int batchSize = Math.Max(32, request.Threads * 4);
                var phrases = new List<string>(batchSize);
                foreach (string phrase in phraseSource)
                {
                    token.ThrowIfCancellationRequested();
                    checkedCombinations++;
                    phrases.Add(phrase);
                    if (phrases.Count < batchSize) continue;
                    result = SearchCpuBatch(phrases, request, ref checksumValid, token);
                    if (result != null)
                    {
                        result.CombinationsChecked = checkedCombinations;
                        result.Engine = engine;
                        break;
                    }
                    phrases.Clear();
                    Report(progress, checkedCombinations, checksumValid, watch, engine);
                }
                if (result == null && phrases.Count > 0)
                {
                    result = SearchCpuBatch(phrases, request, ref checksumValid, token);
                    if (result != null)
                    {
                        result.CombinationsChecked = checkedCombinations;
                        result.Engine = engine;
                    }
                }
            }

            Report(progress, checkedCombinations, checksumValid, watch, engine);
            return result;
        }

        private static MnemonicRecoveryResult SearchEvmGpu(
            MnemonicRecoveryRequest request,
            SolanaVanityGpuGenerator.AddressMatcher matcher,
            RecoveryBatch batch,
            string engine,
            IProgress<MnemonicRecoveryProgress> progress,
            Stopwatch watch,
            ref long checkedCombinations,
            ref long checksumValid,
            CancellationToken token)
        {
            byte[] salt = Bip39Salt(request.Passphrase);
            try
            {
            var phraseSource = EnumeratePhrases(request.PhraseMask, request.CandidateWords,
                request.AllowRepeatedWords, token);
            var phrases = new List<string>(batch.Current);
            foreach (string phrase in phraseSource)
            {
                token.ThrowIfCancellationRequested();
                checkedCombinations++;
                var mnemonic = new Mnemonic(phrase, Wordlist.English);
                if (!mnemonic.IsValidChecksum) continue;
                checksumValid = SaturatingAdd(checksumValid, 1);
                phrases.Add(phrase);
                if (phrases.Count < batch.Current) continue;
                var elapsed = Stopwatch.StartNew();
                var found = SearchEvmSeedBatch(
                    phrases, request, matcher, engine, salt, token);
                batch.Observe(phrases.Count, elapsed.Elapsed.TotalMilliseconds);
                Report(progress, checkedCombinations, checksumValid, watch, engine, phrases.Count);
                if (found != null)
                {
                    found.CombinationsChecked = checkedCombinations;
                    return found;
                }
                phrases.Clear();
            }
            if (phrases.Count > 0)
            {
                var found = SearchEvmSeedBatch(
                    phrases, request, matcher, engine, salt, token);
                Report(progress, checkedCombinations, checksumValid, watch, engine, phrases.Count);
                if (found != null)
                {
                    found.CombinationsChecked = checkedCombinations;
                    return found;
                }
            }
            Report(progress, checkedCombinations, checksumValid, watch, engine);
            return null;
            }
            finally { Array.Clear(salt, 0, salt.Length); }
        }

        private static MnemonicRecoveryResult SearchEvmSeedBatch(
            List<string> phrases,
            MnemonicRecoveryRequest request,
            SolanaVanityGpuGenerator.AddressMatcher matcher,
            string engine,
            byte[] salt,
            CancellationToken token)
        {
            var packed = new byte[checked(phrases.Count * 128)];
            byte[] seeds = null;
            try
            {
                for (int i = 0; i < phrases.Count; i++)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(phrases[i]);
                    try
                    {
                        if (bytes.Length >= 128) throw new InvalidOperationException("助记词长度超过 GPU 缓冲区。");
                        Buffer.BlockCopy(bytes, 0, packed, i * 128, bytes.Length);
                    }
                    finally { Array.Clear(bytes, 0, bytes.Length); }
                }
                seeds = matcher.DeriveSeeds(packed, phrases.Count, salt, token);
                for (int i = 0; i < phrases.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var seed = new byte[64];
                    try
                    {
                        Buffer.BlockCopy(seeds, i * seed.Length, seed, 0, seed.Length);
                        var wallet = new Wallet(seed, "m/44'/60'/0'/0/x");
                        var account = wallet.GetAccount(request.AddressIndex);
                        if (!string.Equals(account.Address, request.TargetAddress.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                        return new MnemonicRecoveryResult {
                            Network = request.Network, Address = account.Address, PrivateKey = account.PrivateKey,
                            Mnemonic = phrases[i], Engine = engine, AddressIndex = request.AddressIndex
                        };
                    }
                    finally { Array.Clear(seed, 0, seed.Length); }
                }
                return null;
            }
            finally
            {
                Array.Clear(packed, 0, packed.Length);
                if (seeds != null) Array.Clear(seeds, 0, seeds.Length);
            }
        }

        private static long SaturatingAdd(long value, int addition)
        {
            return value > long.MaxValue - addition ? long.MaxValue : value + addition;
        }

        private static int[] EnglishIndices(string[] words, bool unknownAsMinusOne)
        {
            string[] english = Wordlist.English.GetWords().ToArray();
            var lookup = english.Select((word, index) => new { word, index })
                .ToDictionary(value => value.word, value => value.index,
                    StringComparer.OrdinalIgnoreCase);
            return words.Select(word => unknownAsMinusOne && IsUnknown(word)
                ? -1 : lookup[word.Trim().ToLowerInvariant()]).ToArray();
        }

        private static byte[] PackEnglishWordList()
        {
            string[] words = Wordlist.English.GetWords().ToArray();
            if (words.Length != 2048)
                throw new InvalidOperationException("英文 BIP39 词表数量不是 2048。");
            var packed = new byte[2048 * 16];
            for (int i = 0; i < words.Length; i++)
            {
                byte[] value = Encoding.UTF8.GetBytes(words[i]);
                if (value.Length >= 16)
                    throw new InvalidOperationException("英文 BIP39 词超过 GPU 词表记录长度。");
                Buffer.BlockCopy(value, 0, packed, i * 16, value.Length);
            }
            return packed;
        }

        private static MnemonicRecoveryResult SearchCpuBatch(
            List<string> phrases,
            MnemonicRecoveryRequest request,
            ref long checksumValid,
            CancellationToken token)
        {
            MnemonicRecoveryResult result = null;
            object gate = new object();
            long validInBatch = checksumValid;
            var options = new ParallelOptions {
                CancellationToken = token,
                MaxDegreeOfParallelism = request.Threads
            };
            Parallel.For(0, phrases.Count, options, i =>
            {
                if (Volatile.Read(ref result) != null) return;
                string phrase = phrases[i];
                var mnemonic = new Mnemonic(phrase, Wordlist.English);
                if (!mnemonic.IsValidChecksum) return;
                Interlocked.Increment(ref validInBatch);
                if (request.Network == "SOL")
                {
                    var wallet = new Solana.Unity.Wallet.Wallet(phrase,
                        Solana.Unity.Wallet.Bip39.WordList.English,
                        request.Passphrase ?? "", Solana.Unity.Wallet.SeedMode.Bip39);
                    if (!string.Equals(wallet.Account.PublicKey.Key,
                        request.TargetAddress.Trim(), StringComparison.Ordinal)) return;
                    var found = new MnemonicRecoveryResult {
                        Network = "SOL", Address = wallet.Account.PublicKey.Key,
                        PrivateKey = wallet.Account.PrivateKey.Key, Mnemonic = phrase,
                        Engine = "CPU", AddressIndex = request.AddressIndex
                    };
                    lock (gate) if (result == null) result = found;
                    return;
                }

                var evmWallet = new Wallet(phrase, request.Passphrase ?? "");
                var account = evmWallet.GetAccount(request.AddressIndex);
                if (!string.Equals(account.Address, request.TargetAddress.Trim(),
                    StringComparison.OrdinalIgnoreCase)) return;
                var evmResult = new MnemonicRecoveryResult {
                    Network = request.Network, Address = account.Address,
                    PrivateKey = account.PrivateKey, Mnemonic = phrase,
                    Engine = "CPU", AddressIndex = request.AddressIndex
                };
                lock (gate) if (result == null) result = evmResult;
            });
            checksumValid = validInBatch;
            return result;
        }

        private static MnemonicRecoveryResult SolanaResult(
            string phrase,
            MnemonicRecoveryRequest request,
            string engine,
            long checkedCombinations)
        {
            var verify = new Solana.Unity.Wallet.Wallet(phrase,
                Solana.Unity.Wallet.Bip39.WordList.English,
                request.Passphrase ?? "", Solana.Unity.Wallet.SeedMode.Bip39);
            string address = verify.Account.PublicKey.Key;
            string privateKey = verify.Account.PrivateKey.Key;
            if (!string.Equals(address, request.TargetAddress.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("GPU 恢复结果助记词复核失败，已丢弃结果。");
            return new MnemonicRecoveryResult {
                Network = "SOL", Address = address, PrivateKey = privateKey,
                Mnemonic = phrase, Engine = engine, AddressIndex = 0,
                CombinationsChecked = checkedCombinations
            };
        }

        private static byte[] Bip39Salt(string passphrase)
        {
            string normalized = (passphrase ?? "").Normalize(NormalizationForm.FormKD);
            byte[] bytes = Encoding.UTF8.GetBytes("mnemonic" + normalized);
            if (bytes.Length > 256)
                throw new ArgumentException("GPU 恢复的 BIP39 密码 UTF-8 长度不能超过 256 字节；请缩短密码或改用 CPU。");
            return bytes;
        }

        private static void Report(
            IProgress<MnemonicRecoveryProgress> progress,
            long checkedCount,
            long checksumValid,
            Stopwatch watch,
            string engine,
            int gpuBatchSize = 0)
        {
            if (progress == null) return;
            progress.Report(new MnemonicRecoveryProgress {
                CombinationsChecked = checkedCount,
                ChecksumValid = checksumValid,
                Engine = engine,
                GpuBatchSize = gpuBatchSize,
                CombinationsPerSecond = checkedCount / Math.Max(0.001, watch.Elapsed.TotalSeconds)
            });
        }

        private static IEnumerable<string> EnumeratePhrases(
            string[] phraseMask,
            string[] candidateWords,
            bool allowRepeatedWords,
            CancellationToken token)
        {
            var words = new string[12];
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 12; i++)
            {
                if (IsUnknown(phraseMask[i])) continue;
                words[i] = phraseMask[i].Trim().ToLowerInvariant();
                if (!allowRepeatedWords) used.Add(words[i]);
            }

            foreach (string phrase in Fill(0))
            {
                token.ThrowIfCancellationRequested();
                yield return phrase;
            }

            IEnumerable<string> Fill(int position)
            {
                if (position == 12)
                {
                    yield return string.Join(" ", words);
                    yield break;
                }
                if (!IsUnknown(phraseMask[position]))
                {
                    foreach (string phrase in Fill(position + 1)) yield return phrase;
                    yield break;
                }
                foreach (string candidate in candidateWords)
                {
                    if (!allowRepeatedWords && used.Contains(candidate)) continue;
                    words[position] = candidate;
                    if (!allowRepeatedWords) used.Add(candidate);
                    foreach (string phrase in Fill(position + 1)) yield return phrase;
                    if (!allowRepeatedWords) used.Remove(candidate);
                }
            }
        }

        private static bool IsUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value == "?" || value == "_";
        }

        private static string FormatScientific(BigInteger value)
        {
            string digits = value.ToString();
            if (digits.Length <= 12) return value.ToString("N0");
            return digits[0] + "." + digits.Substring(1, 2) + "E+" + (digits.Length - 1);
        }
    }
}
