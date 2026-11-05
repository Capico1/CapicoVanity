using System;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Nethereum.Signer;

namespace jmsTools.Manage
{
    // Local vanity generation only. The native engine receives a public key;
    // the random seed private key stays in managed memory.
    internal static class VanityGpuGenerator
    {
        private const string Library = "provanity_cuda_standard.dll";
        private static readonly BigInteger Order = BigInteger.Parse(
            "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141", NumberStyles.HexNumber);
        private static readonly SemaphoreSlim EngineLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim TuningLock = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, uint> TunedBatches = new Dictionary<string, uint>();

        internal static uint ResolveAutomaticBatch(int deviceId, CancellationToken token)
        {
            TuningLock.Wait(token);
            try
            {
                CheckAbi();
                var devices = new Device[16];
                var error = new StringBuilder(512);
                int count = provanity_cuda_list_devices(devices, devices.Length, error, 512);
                var device = devices.Take(Math.Max(0, count)).FirstOrDefault(item => item.Id == deviceId);
                if (string.IsNullOrEmpty(device.Name)) throw new InvalidOperationException("无法识别 NVIDIA GPU：" + error);
                var root = AppDomain.CurrentDomain.BaseDirectory;
                var cache = Path.Combine(AppPaths.DataDirectory, "EvmVanityTuning.json");
                string key = null;
                try
                {
                    var driver = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.SystemDirectory, "nvcuda.dll")).FileVersion;
                    if (!string.IsNullOrEmpty(driver))
                        using (var hash = SHA256.Create())
                        using (var file = File.OpenRead(Path.Combine(root, Library)))
                            key = $"1|{deviceId}|{device.Name}|{device.Memory}|{device.Multiprocessors}|{device.Major}.{device.Minor}|{driver}|{Convert.ToBase64String(hash.ComputeHash(file))}";
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                if (key != null && TunedBatches.TryGetValue(key, out uint known)) return known;
                if (key != null)
                {
                    try
                    {
                        var entry = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cache));
                        var batch = (uint?)entry["batch"];
                        if ((string)entry["key"] == key && batch > 0 && VanityGpuSettings.EvmChoices.Contains(batch.Value))
                            return TunedBatches[key] = batch.Value;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (Newtonsoft.Json.JsonException) { }
                    catch (FormatException) { }
                    catch (InvalidCastException) { }
                    catch (OverflowException) { }
                    catch (ArgumentException) { }
                }

                var scores = new List<KeyValuePair<uint, ulong>>();
                foreach (uint batch in VanityGpuSettings.EvmChoices.Where(value => value > 0))
                {
                    token.ThrowIfCancellationRequested();
                    // Four 32-byte buffers per lane; keep a conservative share of VRAM free.
                    if (batch > 256 && 255UL * batch * 128UL > device.Memory / 8) continue;
                    var samples = new List<ulong>();
                    using (var probe = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        probe.CancelAfter(4000);
                        try
                        {
                            Generate("", new string('0', 40), true, deviceId, probe.Token, (attempts, rate) =>
                            {
                                if (rate == 0) return;
                                samples.Add(rate);
                                if (samples.Count == 4) probe.Cancel();
                            }, message => { }, batch);
                        }
                        catch (OperationCanceledException) when (probe.IsCancellationRequested && !token.IsCancellationRequested) { }
                        catch (InvalidOperationException) { samples.Clear(); }
                    }
                    token.ThrowIfCancellationRequested();
                    if (samples.Count >= 3)
                        scores.Add(new KeyValuePair<uint, ulong>(batch, samples.Skip(1).OrderBy(rate => rate).ElementAt((samples.Count - 1) / 2)));
                }
                if (scores.Count == 0) return 128;
                double threshold = scores.Max(score => score.Value) * 0.97;
                uint chosen = scores.First(score => score.Value >= threshold).Key;
                if (key != null)
                {
                    TunedBatches[key] = chosen;
                    var temporary = cache + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(temporary, new Newtonsoft.Json.Linq.JObject { ["key"] = key, ["batch"] = chosen }.ToString());
                        if (File.Exists(cache)) File.Replace(temporary, cache, null);
                        else File.Move(temporary, cache);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    finally
                    {
                        try { File.Delete(temporary); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                return chosen;
            }
            finally { TuningLock.Release(); }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct Device
        {
            public int Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Name;
            public ulong Memory;
            public int Multiprocessors, Major, Minor;
            public ulong Hashrate;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct NativeEvent
        {
            public int Type;
            public ulong ElapsedSeconds, ElapsedMilliseconds, Attempts, Hashrate;
            public int Score, DeviceCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public Device[] Devices;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 65)] public string Offset;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)] public string Address;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ErrorCode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ErrorMessage;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct Config
        {
            [MarshalAs(UnmanagedType.LPStr)] public string PublicKey;
            public int Mode, Contract;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)] public byte[] Pattern;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public int[] DeviceIds;
            public int DeviceCount;
            public uint BatchMultiple, ProgressInterval, WorkSize;
            public ulong TronSuffixMod;
            public byte StopScore, TronPrefixLevels, TronSuffixLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] TronSuffixDigits;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 640)] public byte[] TronPrefixLadder;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int Callback(IntPtr data, IntPtr userData);

        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.System32)]
        [DllImport(Library, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int provanity_cuda_version(StringBuilder version, uint capacity);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.System32)]
        [DllImport(Library, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int provanity_cuda_list_devices([Out] Device[] devices, int maximum, StringBuilder error, uint capacity);
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.System32)]
        [DllImport(Library, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int provanity_cuda_run(ref Config config, Callback callback, IntPtr userData, StringBuilder error, uint capacity);

        private static readonly object BackendLock = new object();
        private static IntPtr backendHandle;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);

        private static void LoadVerifiedBackend()
        {
            lock (BackendLock)
            {
                if (backendHandle != IntPtr.Zero) return;
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Library);
                if (!File.Exists(path)) throw new DllNotFoundException("未安装可选的 CUDA 组件，将使用 CPU。");
                // Hold the file without write/delete sharing until Windows has mapped it.
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var hash = SHA256.Create())
                {
                    string digest = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "");
                    if (digest != "51BE6F32B7E0F7044F39A86DD7E5B13957A719C0A0B9F12BDD40EDD389530D57")
                        throw new InvalidOperationException("CUDA 组件完整性校验失败，将使用 CPU。");
                    backendHandle = LoadLibraryEx(path, IntPtr.Zero, 0x00000100 | 0x00000800);
                    if (backendHandle == IntPtr.Zero) throw new DllNotFoundException("无法加载 CUDA 组件或显卡驱动。");
                }
            }
        }

        private static void CheckAbi()
        {
            LoadVerifiedBackend();
            if (IntPtr.Size != 8 || Marshal.SizeOf(typeof(Config)) != 800 ||
                Marshal.SizeOf(typeof(Device)) != 168 || Marshal.SizeOf(typeof(NativeEvent)) != 3168)
                throw new InvalidOperationException("GPU 引擎需要 64 位程序，或接口布局不匹配。");
            var version = new StringBuilder(64);
            if (provanity_cuda_version(version, 64) != 0 || version.ToString() != "provanity-cuda/3")
                throw new InvalidOperationException("GPU 引擎版本不匹配。");
        }

        public static string Detect(out int deviceId)
        {
            deviceId = -1;
            try
            {
                CheckAbi();
                var devices = new Device[16];
                var error = new StringBuilder(512);
                int count = provanity_cuda_list_devices(devices, devices.Length, error, 512);
                if (count < 0) return "GPU 不可用：" + error;
                for (int i = 0; i < Math.Min(count, devices.Length); i++)
                {
                    if (devices[i].Major < 7 || (devices[i].Major == 7 && devices[i].Minor < 5)) continue;
                    deviceId = devices[i].Id;
                    return devices[i].Name;
                }
                return "未发现支持的 NVIDIA GPU。";
            }
            catch (DllNotFoundException) { return "GPU 组件或驱动缺失。"; }
            catch (BadImageFormatException) { return "GPU 组件需要 64 位程序。"; }
            catch (Exception ex) { return "GPU 不可用：" + ex.Message; }
        }

        internal static bool IsGpuFailure(Exception error) =>
            error is InvalidOperationException || error is DllNotFoundException ||
            error is EntryPointNotFoundException || error is BadImageFormatException ||
            error is PlatformNotSupportedException;

        public static bool Matches(string address, string prefix, string suffix, bool matchCase)
        {
            string value = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address.Substring(2) : address;
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return value.StartsWith(prefix, comparison) && value.EndsWith(suffix, comparison);
        }

        internal static string ComposePrivateKey(string seed, string offset)
        {
            var value = (ParseHex(seed) + ParseHex(offset)) % Order;
            if (value.IsZero) throw new InvalidOperationException("GPU 返回了无效私钥。");
            string hex = value.ToString("x", CultureInfo.InvariantCulture);
            return hex.Length > 64 ? hex.Substring(hex.Length - 64) : hex.PadLeft(64, '0');
        }

        private static BigInteger ParseHex(string value)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value.Substring(2);
            if (value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidOperationException("GPU 返回了无效偏移量。");
            return BigInteger.Parse("0" + value, NumberStyles.HexNumber);
        }

        public sealed class Result
        {
            public string Address, PrivateKey;
        }

        public static Result GenerateTemplate(VanityTemplate template, bool matchCase, int deviceId,
            CancellationToken token, Action<long, ulong> progress, Action<string> status, uint batchMultiple = 256)
        {
            string literal = template.CreateEvmLiteral();
            var result = Generate(template.AtEnd ? "" : literal, template.AtEnd ? literal : "", matchCase,
                deviceId, token, progress, status, batchMultiple);
            if (!template.Matches(result.Address, false, matchCase))
                throw new InvalidOperationException("EVM 模板匹配校验失败。");
            return result;
        }

        public static Result GenerateTemplateSet(VanityTemplate[] templates, bool matchCase, int deviceId,
            CancellationToken token, Action<long, ulong> progress, Action<int> onTemplate, int startIndex, uint batchMultiple)
        {
            if (batchMultiple == 0) batchMultiple = ResolveAutomaticBatch(deviceId, token);
            long total = 0;
            for (int index = startIndex; ; index = (index + 1) % templates.Length)
            {
                token.ThrowIfCancellationRequested();
                onTemplate(index);
                long attempts = 0;
                using (var slice = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    slice.CancelAfter(5000);
                    try
                    {
                        return GenerateTemplate(templates[index], matchCase, deviceId, slice.Token, (count, rate) => {
                            attempts = count;
                            progress(total + count, rate);
                        }, message => { }, batchMultiple);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested && slice.IsCancellationRequested) { }
                }
                total += attempts;
            }
        }

        public static Result Generate(string prefix, string suffix, bool matchCase, int deviceId,
            CancellationToken token, Action<long, ulong> progress, Action<string> status, uint batchMultiple = 256)
        {
            VanityGpuSettings.ValidateEvm(batchMultiple);
            if (batchMultiple == 0) batchMultiple = ResolveAutomaticBatch(deviceId, token);
            // Native CUDA constants are shared within a process; serialize calls.
            EngineLock.Wait(token);
            try
            {
                CheckAbi();
                long total = 0;
                int targetScore = prefix.Length + suffix.Length;
                string searchPrefix = prefix;
                if (targetScore == 0)
                {
                    var random = new byte[1];
                    using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
                    searchPrefix = "0123456789abcdef"[random[0] & 0x0f].ToString();
                    targetScore = 1;
                }
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var seed = EthECKey.GenerateKey();
                    var publicKey = seed.GetPubKey();
                    var config = new Config {
                        PublicKey = BitConverter.ToString(publicKey.Skip(1).ToArray()).Replace("-", ""),
                        Mode = 1, Pattern = Enumerable.Repeat((byte)255, 40).ToArray(),
                        DeviceIds = new int[16], DeviceCount = 1, BatchMultiple = batchMultiple,
                        ProgressInterval = 250, WorkSize = 128,
                        TronSuffixDigits = new byte[8], TronPrefixLadder = new byte[640]
                    };
                    config.DeviceIds[0] = deviceId;
                    for (int i = 0; i < searchPrefix.Length; i++) config.Pattern[i] = Convert.ToByte(searchPrefix.Substring(i, 1), 16);
                    for (int i = 0; i < suffix.Length; i++) config.Pattern[40 - suffix.Length + i] = Convert.ToByte(suffix.Substring(i, 1), 16);
                    Result result = null;
                    Exception failure = null;
                    ulong attempts = 0;
                    bool retry = false;
                    Callback callback = (data, unused) => {
                        if (token.IsCancellationRequested) return 1;
                        try
                        {
                            var ev = (NativeEvent)Marshal.PtrToStructure(data, typeof(NativeEvent));
                            if (ev.Type == 2 || ev.Type == 3)
                            {
                                attempts = Math.Max(attempts, ev.Attempts);
                                progress(total + (long)attempts, ev.Hashrate);
                            }
                            if (ev.Type == 5) status("GPU 初始化中…");
                            if (ev.Type == 4) throw new InvalidOperationException(ev.ErrorMessage);
                            if (ev.Type != 3 || ev.Score < targetScore) return 0;
                            var privateKey = ComposePrivateKey(seed.GetPrivateKey(), ev.Offset);
                            var address = new EthECKey(privateKey).GetPublicAddress();
                            if (!string.Equals(address.Substring(2), ev.Address, StringComparison.OrdinalIgnoreCase) ||
                                !Matches(address, prefix, suffix, false))
                                throw new InvalidOperationException("GPU 结果校验失败，已丢弃结果。");
                            if (Matches(address, prefix, suffix, matchCase))
                                result = new Result { Address = address, PrivateKey = privateKey };
                            else retry = true; // New public seed after a checksum-case mismatch.
                            return 1;
                        }
                        catch (Exception ex) { failure = ex; return 1; }
                    };
                    var error = new StringBuilder(512);
                    int code = provanity_cuda_run(ref config, callback, IntPtr.Zero, error, 512);
                    GC.KeepAlive(callback);
                    token.ThrowIfCancellationRequested();
                    if (failure != null) throw failure;
                    if (code < 0) throw new InvalidOperationException(error.ToString());
                    if (result != null) return result;
                    if (!retry) throw new InvalidOperationException("GPU 搜索意外结束，未返回有效结果。");
                    total += (long)attempts;
                }
            }
            finally { EngineLock.Release(); }
        }
    }
}
