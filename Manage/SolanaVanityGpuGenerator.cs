using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NBitcoin.DataEncoders;

namespace jmsTools.Manage
{
    internal static class SolanaVanityGpuGenerator
    {
        private const string Library = "OpenCL.dll";
        private static readonly SemaphoreSlim EngineLock = new SemaphoreSlim(1, 1);
        private static VanitySession cachedVanitySession;

        static SolanaVanityGpuGenerator()
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
                if (!EngineLock.Wait(0)) return;
                try { cachedVanitySession?.Dispose(); cachedVanitySession = null; }
                finally { EngineLock.Release(); }
            };
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetPlatformIDs(uint count, [Out] IntPtr[] platforms, out uint actual);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetDeviceIDs(IntPtr platform, ulong type, uint count, [Out] IntPtr[] devices, out uint actual);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetDeviceInfo(IntPtr device, uint name, UIntPtr size, byte[] value, out UIntPtr actual);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern IntPtr clCreateContext(IntPtr properties, uint count, IntPtr[] devices, IntPtr notify, IntPtr user, out int error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern IntPtr clCreateCommandQueue(IntPtr context, IntPtr device, ulong properties, out int error);
        [DllImport(Library, CharSet = CharSet.Ansi)] private static extern IntPtr clCreateProgramWithSource(IntPtr context, uint count,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] source, UIntPtr[] lengths, out int error);
        [DllImport(Library, CharSet = CharSet.Ansi)] private static extern int clBuildProgram(IntPtr program, uint count, IntPtr[] devices, string options, IntPtr notify, IntPtr user);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetProgramBuildInfo(IntPtr program, IntPtr device, uint name, UIntPtr size, byte[] value, out UIntPtr actual);
        [DllImport(Library, CharSet = CharSet.Ansi)] private static extern IntPtr clCreateKernel(IntPtr program, string name, out int error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern IntPtr clCreateBuffer(IntPtr context, ulong flags, UIntPtr size, IntPtr host, out int error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clSetKernelArg(IntPtr kernel, uint index, UIntPtr size, ref IntPtr value);
        [DllImport(Library, EntryPoint = "clSetKernelArg")] private static extern int SetIntArg(IntPtr kernel, uint index, UIntPtr size, ref int value);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clEnqueueWriteBuffer(IntPtr queue, IntPtr buffer, uint blocking, UIntPtr offset, UIntPtr size, byte[] data, uint count, IntPtr waits, IntPtr ev);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clEnqueueReadBuffer(IntPtr queue, IntPtr buffer, uint blocking, UIntPtr offset, UIntPtr size, byte[] data, uint count, IntPtr waits, IntPtr ev);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clEnqueueNDRangeKernel(IntPtr queue, IntPtr kernel, uint dims, UIntPtr[] offset, UIntPtr[] global, UIntPtr[] local, uint count, IntPtr waits, IntPtr ev);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clReleaseMemObject(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clReleaseKernel(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clReleaseProgram(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clReleaseCommandQueue(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clReleaseContext(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetKernelWorkGroupInfo(IntPtr kernel, IntPtr device, uint name, UIntPtr size, out UIntPtr value, out UIntPtr actual);
        [DllImport(Library, EntryPoint = "clEnqueueNDRangeKernel")] private static extern int EnqueueProfiled(IntPtr queue, IntPtr kernel, uint dims, UIntPtr[] offset, UIntPtr[] global, UIntPtr[] local, uint count, IntPtr waits, out IntPtr ev);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetEventProfilingInfo(IntPtr ev, uint name, UIntPtr size, out ulong value, out UIntPtr actual);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clReleaseEvent(IntPtr ev);

        private static void Check(int error, string operation)
        {
            if (error != 0) throw new InvalidOperationException("SOL GPU " + operation + "失败（OpenCL " + error + "）。");
        }

        public static string Detect(out IntPtr device)
        {
            device = IntPtr.Zero;
            try
            {
                uint count;
                Check(clGetPlatformIDs(0, null, out count), "平台检测");
                var platforms = new IntPtr[count];
                Check(clGetPlatformIDs(count, platforms, out count), "平台检测");
                string selected = null;
                foreach (var platform in platforms)
                {
                    uint devicesCount;
                    if (clGetDeviceIDs(platform, 4, 0, null, out devicesCount) != 0 || devicesCount == 0) continue;
                    var devices = new IntPtr[devicesCount];
                    if (clGetDeviceIDs(platform, 4, devicesCount, devices, out devicesCount) != 0) continue;
                    foreach (var candidate in devices)
                    {
                        if (candidate == IntPtr.Zero) continue;
                        var status = new byte[4];
                        if (clGetDeviceInfo(candidate, 0x1027, (UIntPtr)4, status, out _) != 0 || BitConverter.ToUInt32(status, 0) == 0 ||
                            clGetDeviceInfo(candidate, 0x1028, (UIntPtr)4, status, out _) != 0 || BitConverter.ToUInt32(status, 0) == 0)
                            continue; // CL_DEVICE_AVAILABLE and CL_DEVICE_COMPILER_AVAILABLE
                        var bytes = new byte[512]; UIntPtr length;
                        if (clGetDeviceInfo(candidate, 0x102B, (UIntPtr)bytes.Length, bytes, out length) != 0) continue;
                        string name = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                        if (device == IntPtr.Zero || name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                        { device = candidate; selected = name; }
                        if (name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) return name;
                    }
                }
                return selected ?? "未检测到 OpenCL GPU。";
            }
            catch (Exception ex) { device = IntPtr.Zero; return "OpenCL GPU 不可用：" + ex.Message; }
        }

        private static IntPtr Buffer(IntPtr context, int length)
        {
            int error;
            var buffer = clCreateBuffer(context, 1, (UIntPtr)length, IntPtr.Zero, out error);
            Check(error, "显存分配"); return buffer;
        }

        private static void Write(IntPtr queue, IntPtr buffer, byte[] bytes)
        {
            Check(clEnqueueWriteBuffer(queue, buffer, 1, UIntPtr.Zero, (UIntPtr)bytes.Length, bytes, 0, IntPtr.Zero, IntPtr.Zero), "写入");
        }

        private static void Write(IntPtr queue, IntPtr buffer, byte[] bytes, int length)
        {
            Check(clEnqueueWriteBuffer(queue, buffer, 1, UIntPtr.Zero, (UIntPtr)length, bytes, 0, IntPtr.Zero, IntPtr.Zero), "写入");
        }

        internal sealed class VanityMetrics
        {
            public int BatchSize, WorkGroupSize;
            public long Candidates;
            public bool SessionReused, TuningReused;
            public double InitializationMilliseconds, SearchMilliseconds, KernelMilliseconds, BatchMilliseconds;
        }

        private sealed class TuningProfile
        {
            public string Identity { get; set; }
            public int WorkGroupSize { get; set; }
            public int BatchSize { get; set; }
        }

        // Only the vanity engine reuses this session. Recovery and other GPU
        // features retain their own queues and buffers. EngineLock guards all use.
        private sealed class VanitySession : IDisposable
        {
            public readonly IntPtr Device;
            private IntPtr context, queue, program, kernel, seedBuffer, outputBuffer, patternBuffer;
            private readonly byte[] output = new byte[68], zeros = new byte[68], seed = new byte[32];
            private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
            private readonly string identity;
            private readonly int maximumGroup;
            private readonly int preferredGroup;
            private TuningProfile tuning;
            public double KernelMilliseconds;

            public VanitySession(IntPtr device, CancellationToken token)
            {
                Device = device;
                try
                {
                    int error;
                    token.ThrowIfCancellationRequested();
                    context = clCreateContext(IntPtr.Zero, 1, new[] { device }, IntPtr.Zero, IntPtr.Zero, out error); Check(error, "上下文创建");
                    queue = clCreateCommandQueue(context, device, 2, out error); Check(error, "队列创建");
                    string source;
                    using (var stream = typeof(SolanaVanityGpuGenerator).Assembly.GetManifestResourceStream("jmsTools.SolanaVanityKernel.cl"))
                    using (var reader = new StreamReader(stream)) source = reader.ReadToEnd();
                    using (var hash = SHA256.Create())
                        identity = DeviceText(device, 0x102C) + "|" + DeviceText(device, 0x102B) + "|" + DeviceText(device, 0x102D) + "|" +
                            BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source))) + "|vanity-only|tune-v1";
                    program = clCreateProgramWithSource(context, 1, new[] { source }, null, out error); Check(error, "程序创建");
                    error = clBuildProgram(program, 1, new[] { device }, "-DJMS_VANITY_ONLY=1", IntPtr.Zero, IntPtr.Zero);
                    if (error != 0)
                    {
                        UIntPtr length;
                        clGetProgramBuildInfo(program, device, 0x1183, UIntPtr.Zero, null, out length);
                        var log = new byte[checked((int)length.ToUInt64())];
                        clGetProgramBuildInfo(program, device, 0x1183, (UIntPtr)log.Length, log, out length);
                        throw new InvalidOperationException("SOL GPU 编译失败：" + Encoding.UTF8.GetString(log.Take(16000).ToArray()).TrimEnd('\0'));
                    }
                    token.ThrowIfCancellationRequested();
                    kernel = clCreateKernel(program, "jms_sol_vanity", out error); Check(error, "内核创建");
                    UIntPtr value, actual;
                    Check(clGetKernelWorkGroupInfo(kernel, device, 0x11B0, (UIntPtr)IntPtr.Size, out value, out actual), "工作组上限");
                    maximumGroup = (int)Math.Min(1024UL, value.ToUInt64());
                    preferredGroup = clGetKernelWorkGroupInfo(kernel, device, 0x11B3, (UIntPtr)IntPtr.Size, out value, out actual) == 0
                        ? (int)Math.Min(1024UL, value.ToUInt64()) : 32;
                    seedBuffer = Buffer(context, 32); outputBuffer = Buffer(context, 68); patternBuffer = Buffer(context, 88);
                    Check(clSetKernelArg(kernel, 0, (UIntPtr)IntPtr.Size, ref seedBuffer), "种子参数");
                    Check(clSetKernelArg(kernel, 1, (UIntPtr)IntPtr.Size, ref outputBuffer), "结果参数");
                    Check(clSetKernelArg(kernel, 2, (UIntPtr)IntPtr.Size, ref patternBuffer), "匹配参数");
                }
                catch { Dispose(); throw; }
            }

            private static string DeviceText(IntPtr device, uint name)
            {
                var bytes = new byte[1024]; UIntPtr actual;
                Check(clGetDeviceInfo(device, name, (UIntPtr)bytes.Length, bytes, out actual), "设备信息");
                return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            }

            public void SetPattern(string prefix, string suffix, bool matchCase, VanityTemplate template = null, VanityTemplate[] additional = null)
            {
                int p = prefix.Length, s = suffix.Length, sensitive = matchCase ? 1 : 0;
                Check(SetIntArg(kernel, 3, (UIntPtr)4, ref p), "前缀参数");
                Check(SetIntArg(kernel, 4, (UIntPtr)4, ref s), "后缀参数");
                Check(SetIntArg(kernel, 5, (UIntPtr)4, ref sensitive), "大小写参数");
                int kind = template?.Kind ?? 0, length = template?.Length ?? 0, tail = template != null && template.AtEnd ? 1 : 0;
                var pattern = new byte[88];
                if (additional != null && additional.Length > 0)
                {
                    kind = 8;
                    length = additional.Length + 1;
                    var templates = new[] { template }.Concat(additional).ToArray();
                    for (int i = 0; i < templates.Length; i++)
                    {
                        pattern[14 * i] = (byte)templates[i].Kind;
                        pattern[14 * i + 1] = (byte)templates[i].Length;
                        Encoding.ASCII.GetBytes(templates[i].Pattern).CopyTo(pattern, 14 * i + 2);
                    }
                }
                else Encoding.ASCII.GetBytes(template == null ? prefix + suffix : template.Pattern).CopyTo(pattern, 0);
                Check(SetIntArg(kernel, 6, (UIntPtr)4, ref kind), "模板类型");
                Check(SetIntArg(kernel, 7, (UIntPtr)4, ref length), "模板长度");
                Check(SetIntArg(kernel, 8, (UIntPtr)4, ref tail), "模板位置");
                Write(queue, patternBuffer, pattern);
            }

            private bool ValidGroup(int group) => group == 0 ||
                (group > 0 && group <= maximumGroup && 16384 % group == 0);

            public byte[] RunBatch(int batch, int group, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (!ValidGroup(group)) throw new ArgumentException("SOL 工作组大小不受当前内核支持。");
                random.GetBytes(seed); Write(queue, seedBuffer, seed); Write(queue, outputBuffer, zeros);
                IntPtr ev = IntPtr.Zero;
                try
                {
                    Check(EnqueueProfiled(queue, kernel, 1, null, new[] { (UIntPtr)batch }, group == 0 ? null : new[] { (UIntPtr)group }, 0, IntPtr.Zero, out ev), "搜索");
                    Check(clEnqueueReadBuffer(queue, outputBuffer, 1, UIntPtr.Zero, (UIntPtr)output.Length, output, 0, IntPtr.Zero, IntPtr.Zero), "读取结果");
                    ulong start, end; UIntPtr actual;
                    KernelMilliseconds = clGetEventProfilingInfo(ev, 0x1282, (UIntPtr)8, out start, out actual) == 0 &&
                        clGetEventProfilingInfo(ev, 0x1283, (UIntPtr)8, out end, out actual) == 0 && end >= start
                        ? (end - start) / 1000000.0 : 0;
                    token.ThrowIfCancellationRequested();
                    return output;
                }
                finally { if (ev != IntPtr.Zero) clReleaseEvent(ev); }
            }

            public void SelectConfiguration(CancellationToken token, out int batch, out int group, out bool reused)
            {
                reused = true;
                if (tuning == null)
                {
                    try
                    {
                        var cached = Newtonsoft.Json.JsonConvert.DeserializeObject<TuningProfile>(File.ReadAllText(TuningPath));
                        if (cached != null && cached.Identity == identity && ValidGroup(cached.WorkGroupSize) &&
                            cached.BatchSize > 0 && VanityGpuSettings.SolChoices.Contains(cached.BatchSize)) tuning = cached;
                    }
                    catch { /* Read-only directories and old/corrupt caches are harmless. */ }
                }
                if (tuning == null)
                {
                    reused = false;
                    // No valid Base58 address can contain '0'. Calibration never
                    // competes for a user's match or contributes to search counts.
                    SetPattern("0", "", true);
                    var groups = new[] { 0, preferredGroup, 32, 64, 128, 256 }
                        .Where(ValidGroup).Distinct().ToArray();
                    foreach (var candidate in groups) RunBatch(16384, candidate, token);
                    var samples = groups.ToDictionary(candidate => candidate, candidate => new List<double>());
                    for (int round = 0; round < 3; round++)
                        foreach (var candidate in round % 2 == 0 ? groups : groups.Reverse())
                        {
                            var watch = Stopwatch.StartNew(); RunBatch(65536, candidate, token);
                            samples[candidate].Add(watch.Elapsed.TotalMilliseconds);
                        }
                    int chosenGroup = groups.OrderBy(candidate => Median(samples[candidate])).First();
                    var batches = new[] { 16384, 65536, 262144 };
                    var rates = batches.ToDictionary(candidate => candidate, candidate => new List<double>());
                    for (int round = 0; round < 3; round++)
                        foreach (var candidate in round % 2 == 0 ? batches : batches.Reverse())
                        {
                            var watch = Stopwatch.StartNew(); RunBatch(candidate, chosenGroup, token);
                            rates[candidate].Add(candidate / Math.Max(0.000001, watch.Elapsed.TotalSeconds));
                        }
                    // Prefer a responsive batch on slower adapters; do not
                    // trade seconds of stop latency for a small throughput gain.
                    var responsive = batches.Where(candidate => candidate / Median(rates[candidate]) <= 0.1).ToArray();
                    if (responsive.Length == 0) responsive = new[] { batches[0] };
                    double best = responsive.Max(candidate => Median(rates[candidate]));
                    int chosenBatch = responsive.First(candidate => Median(rates[candidate]) >= best * 0.97);
                    tuning = new TuningProfile { Identity = identity, WorkGroupSize = chosenGroup, BatchSize = chosenBatch };
                    // This file contains hardware/performance settings only.
                    var temporary = TuningPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(temporary, Newtonsoft.Json.JsonConvert.SerializeObject(tuning), new UTF8Encoding(false));
                        if (File.Exists(TuningPath)) File.Replace(temporary, TuningPath, null);
                        else File.Move(temporary, TuningPath);
                    }
                    catch { }
                    finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
                }
                batch = tuning.BatchSize; group = tuning.WorkGroupSize;
            }

            private static string TuningPath => Path.Combine(AppPaths.DataDirectory, "SolVanityTuning.json");
            private static double Median(List<double> values) => values.OrderBy(value => value).ElementAt(values.Count / 2);

            public void ClearSecrets()
            {
                Array.Clear(seed, 0, seed.Length); Array.Clear(output, 0, output.Length);
                if (queue != IntPtr.Zero && seedBuffer != IntPtr.Zero) Write(queue, seedBuffer, seed);
                if (queue != IntPtr.Zero && outputBuffer != IntPtr.Zero) Write(queue, outputBuffer, zeros);
            }

            public void Dispose()
            {
                if (patternBuffer != IntPtr.Zero) { clReleaseMemObject(patternBuffer); patternBuffer = IntPtr.Zero; }
                if (outputBuffer != IntPtr.Zero) { clReleaseMemObject(outputBuffer); outputBuffer = IntPtr.Zero; }
                if (seedBuffer != IntPtr.Zero) { clReleaseMemObject(seedBuffer); seedBuffer = IntPtr.Zero; }
                if (kernel != IntPtr.Zero) { clReleaseKernel(kernel); kernel = IntPtr.Zero; }
                if (program != IntPtr.Zero) { clReleaseProgram(program); program = IntPtr.Zero; }
                if (queue != IntPtr.Zero) { clReleaseCommandQueue(queue); queue = IntPtr.Zero; }
                if (context != IntPtr.Zero) { clReleaseContext(context); context = IntPtr.Zero; }
                Array.Clear(seed, 0, seed.Length); Array.Clear(output, 0, output.Length);
                random.Dispose();
            }
        }

        public static VanityGpuGenerator.Result Generate(string prefix, string suffix, bool matchCase,
            IntPtr device, CancellationToken token, Action<long, ulong> progress, int batchSize = 65536,
            Action<VanityMetrics> diagnostics = null, int workGroupSize = -1, VanityTemplate template = null, VanityTemplate[] additional = null)
        {
            VanityGpuSettings.ValidateSol(batchSize);
            if (workGroupSize < -1) throw new ArgumentOutOfRangeException(nameof(workGroupSize));
            string patternError = SolanaVanityGenerator.ValidatePattern(prefix, suffix, matchCase);
            if (patternError != null) throw new ArgumentException(patternError);
            if (template != null && (prefix.Length != 0 || suffix.Length != 0)) throw new ArgumentException("模板与固定前后缀应分别使用。");
            if (additional != null && additional.Length > 0 && (template == null || additional.Length > 5 || additional.Any(item => item == null || item.AtEnd != template.AtEnd)))
                throw new ArgumentException("最多可添加 5 个位置相同的附加模板。");
            if (device == IntPtr.Zero) throw new InvalidOperationException("未选择 SOL GPU。");
            EngineLock.Wait(token);
            try
            {
                token.ThrowIfCancellationRequested();
                var initialization = Stopwatch.StartNew();
                bool reused = cachedVanitySession != null && cachedVanitySession.Device == device;
                if (!reused)
                {
                    cachedVanitySession?.Dispose(); cachedVanitySession = null;
                    cachedVanitySession = new VanitySession(device, token);
                }
                var session = cachedVanitySession;
                int tunedBatch = 65536, group = workGroupSize;
                bool tuningReused = false;
                if (workGroupSize == -1 || batchSize == 0)
                    session.SelectConfiguration(token, out tunedBatch, out group, out tuningReused);
                if (workGroupSize >= 0) group = workGroupSize;
                if (batchSize == 0) batchSize = tunedBatch;
                session.SetPattern(prefix, suffix, matchCase, template, additional);
                initialization.Stop();
                var search = Stopwatch.StartNew();
                long total = 0;
                while (true)
                {
                    var batchWatch = Stopwatch.StartNew();
                    var output = session.RunBatch(batchSize, group, token);
                    batchWatch.Stop();
                    total += batchSize;
                    // Wall-clock search throughput includes seed generation,
                    // both transfers, synchronization, and previous callbacks.
                    progress(total, (ulong)(total / Math.Max(0.000001, search.Elapsed.TotalSeconds)));
                    diagnostics?.Invoke(new VanityMetrics {
                        BatchSize = batchSize, WorkGroupSize = group, Candidates = total,
                        SessionReused = reused, TuningReused = tuningReused,
                        InitializationMilliseconds = initialization.Elapsed.TotalMilliseconds,
                        SearchMilliseconds = search.Elapsed.TotalMilliseconds,
                        KernelMilliseconds = session.KernelMilliseconds, BatchMilliseconds = batchWatch.Elapsed.TotalMilliseconds
                    });
                    if (BitConverter.ToUInt32(output, 0) == 0) continue;
                    var foundSeed = output.Skip(4).Take(32).ToArray();
                    try
                    {
                        var publicKey = Chaos.NaCl.Ed25519.PublicKeyFromSeed(foundSeed);
                        if (!publicKey.SequenceEqual(output.Skip(36))) throw new InvalidOperationException("SOL GPU 地址派生校验失败。");
                        var result = new VanityGpuGenerator.Result {
                            Address = Encoders.Base58.EncodeData(publicKey),
                            PrivateKey = Encoders.Base58.EncodeData(Chaos.NaCl.Ed25519.ExpandedPrivateKeyFromSeed(foundSeed))
                        };
                        SolanaVanityGenerator.VerifyResult(result);
                        if (!(template == null ? SolanaVanityGenerator.Matches(result.Address, prefix, suffix, matchCase) :
                            template.Matches(result.Address, true, matchCase) || (additional != null && additional.Any(item => item.Matches(result.Address, true, matchCase)))))
                            throw new InvalidOperationException("SOL GPU 前后缀校验失败。");
                        return result;
                    }
                    finally { Array.Clear(foundSeed, 0, foundSeed.Length); }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                cachedVanitySession?.Dispose(); cachedVanitySession = null;
                throw;
            }
            finally
            {
                try { cachedVanitySession?.ClearSecrets(); }
                catch { cachedVanitySession?.Dispose(); cachedVanitySession = null; }
                EngineLock.Release();
            }
        }

        internal sealed class GeneratedSearchResult
        {
            public int Index;
            public int ChecksumValid;
            public string Phrase;
        }

        // Batch recovery path used by the local mnemonic page. BIP39 seed
        // derivation and Ed25519 matching run in one OpenCL command queue.
        internal sealed class AddressMatcher : IDisposable
        {
            private IntPtr context, queue, program, generateKernel, deriveKernel, matchKernel;
            private IntPtr wordListBuffer, fixedBuffer, candidateBuffer, startRankBuffer;
            private IntPtr phraseBuffer, saltBuffer, seedsBuffer, validBuffer;
            private IntPtr targetBuffer, outputBuffer;
            private readonly int batchCapacity;
            private bool disposed;

            public AddressMatcher(IntPtr device, int batchCapacity) : this(device, batchCapacity, CancellationToken.None) { }

            public AddressMatcher(IntPtr device, int batchCapacity, CancellationToken token)
            {
                if (device == IntPtr.Zero) throw new ArgumentException("未选择 SOL GPU。");
                if (batchCapacity < 1 || batchCapacity > 1048576)
                    throw new ArgumentOutOfRangeException("batchCapacity");
                this.batchCapacity = batchCapacity;
                EngineLock.Wait(token);
                try
                {
                    int error;
                    context = clCreateContext(IntPtr.Zero, 1, new[] { device }, IntPtr.Zero, IntPtr.Zero, out error);
                    Check(error, "恢复上下文创建");
                    queue = clCreateCommandQueue(context, device, 0, out error);
                    Check(error, "恢复队列创建");
                    string source;
                    using (var stream = typeof(SolanaVanityGpuGenerator).Assembly.GetManifestResourceStream("jmsTools.SolanaVanityKernel.cl"))
                    using (var reader = new StreamReader(stream)) source = reader.ReadToEnd();
                    program = clCreateProgramWithSource(context, 1, new[] { source }, null, out error);
                    Check(error, "恢复程序创建");
                    error = clBuildProgram(program, 1, new[] { device }, "", IntPtr.Zero, IntPtr.Zero);
                    if (error != 0)
                    {
                        UIntPtr length;
                        clGetProgramBuildInfo(program, device, 0x1183, UIntPtr.Zero, null, out length);
                        var log = new byte[(int)length.ToUInt64()];
                        clGetProgramBuildInfo(program, device, 0x1183, (UIntPtr)log.Length, log, out length);
                        throw new InvalidOperationException("SOL 恢复内核编译失败：" +
                            Encoding.UTF8.GetString(log.Take(16000).ToArray()).TrimEnd('\0'));
                    }
                    deriveKernel = clCreateKernel(program, "jms_sol_pbkdf2_seeds", out error);
                    Check(error, "恢复种子内核创建");
                    matchKernel = clCreateKernel(program, "jms_sol_match", out error);
                    Check(error, "恢复地址内核创建");
                    generateKernel = clCreateKernel(program, "jms_sol_generate_mnemonics", out error);
                    Check(error, "恢复词组合内核创建");
                    wordListBuffer = Buffer(context, 2048 * 16);
                    fixedBuffer = Buffer(context, 64);
                    candidateBuffer = Buffer(context, 2048 * 4);
                    startRankBuffer = Buffer(context, 3 * 8);
                    phraseBuffer = Buffer(context, checked(batchCapacity * 128));
                    saltBuffer = Buffer(context, 256);
                    seedsBuffer = Buffer(context, checked(batchCapacity * 64));
                    validBuffer = Buffer(context, checked(batchCapacity * 4));
                    targetBuffer = Buffer(context, 64);
                    outputBuffer = Buffer(context, 4);
                    Check(clSetKernelArg(generateKernel, 0, (UIntPtr)IntPtr.Size, ref wordListBuffer), "恢复词表参数");
                    Check(clSetKernelArg(generateKernel, 1, (UIntPtr)IntPtr.Size, ref fixedBuffer), "恢复固定词参数");
                    Check(clSetKernelArg(generateKernel, 2, (UIntPtr)IntPtr.Size, ref candidateBuffer), "恢复候选词参数");
                    Check(clSetKernelArg(generateKernel, 3, (UIntPtr)IntPtr.Size, ref startRankBuffer), "恢复起始序号参数");
                    Check(clSetKernelArg(generateKernel, 7, (UIntPtr)IntPtr.Size, ref phraseBuffer), "恢复助记词输出参数");
                    Check(clSetKernelArg(generateKernel, 8, (UIntPtr)IntPtr.Size, ref validBuffer), "恢复校验结果参数");
                    Check(clSetKernelArg(deriveKernel, 0, (UIntPtr)IntPtr.Size, ref phraseBuffer), "恢复助记词参数");
                    Check(clSetKernelArg(deriveKernel, 1, (UIntPtr)IntPtr.Size, ref saltBuffer), "恢复密码盐参数");
                    Check(clSetKernelArg(deriveKernel, 4, (UIntPtr)IntPtr.Size, ref seedsBuffer), "恢复种子输出参数");
                    Check(clSetKernelArg(deriveKernel, 6, (UIntPtr)IntPtr.Size, ref validBuffer), "恢复种子校验参数");
                    Check(clSetKernelArg(matchKernel, 0, (UIntPtr)IntPtr.Size, ref seedsBuffer), "恢复种子重算参数");
                    Check(clSetKernelArg(matchKernel, 1, (UIntPtr)IntPtr.Size, ref targetBuffer), "恢复地址参数");
                    Check(clSetKernelArg(matchKernel, 5, (UIntPtr)IntPtr.Size, ref outputBuffer), "恢复结果参数");
                    Check(clSetKernelArg(matchKernel, 6, (UIntPtr)IntPtr.Size, ref validBuffer), "恢复地址校验参数");
                }
                catch
                {
                    ReleaseResources();
                    EngineLock.Release();
                    throw;
                }
            }

            public int Match(byte[] packedSeeds, int count, string target, CancellationToken token)
            {
                if (disposed) throw new ObjectDisposedException("SolanaAddressMatcher");
                if (count < 1 || count > batchCapacity) throw new ArgumentOutOfRangeException("count");
                if (packedSeeds == null || packedSeeds.Length < count * 32) throw new ArgumentException("种子缓冲区不足。");
                if (string.IsNullOrEmpty(target) || target.Length > 63) throw new ArgumentException("SOL 地址格式无效。");
                token.ThrowIfCancellationRequested();
                WriteValidFlags(count, true);
                Write(queue, seedsBuffer, packedSeeds, count * 32);
                return RunMatch(target, count, 32, token);
            }

            public int MatchPhrases(byte[] packedPhrases, int count, string target,
                byte[] salt, CancellationToken token)
            {
                if (string.IsNullOrEmpty(target) || target.Length > 63)
                    throw new ArgumentException("SOL 地址格式无效。");
                WriteValidFlags(count, true);
                LaunchDerive(packedPhrases, count, salt, token);
                return RunMatch(target, count, 64, token);
            }

            public GeneratedSearchResult MatchGenerated(
                byte[] wordList,
                int[] fixedIndices,
                int[] candidateIndices,
                BigInteger startRank,
                int count,
                string target,
                byte[] salt,
                CancellationToken token)
            {
                if (disposed) throw new ObjectDisposedException("SolanaAddressMatcher");
                if (wordList == null || wordList.Length != 2048 * 16)
                    throw new ArgumentException("英文 BIP39 GPU 词表必须为 2048 × 16 字节。");
                if (fixedIndices == null || fixedIndices.Length != 12)
                    throw new ArgumentException("固定词索引必须正好包含 12 项。");
                if (candidateIndices == null || candidateIndices.Length > 2048)
                    throw new ArgumentException("候选词过多。");
                if (startRank.Sign < 0 || startRank.ToByteArray().Length > 24)
                    throw new ArgumentOutOfRangeException("startRank");
                if (count < 1 || count > batchCapacity) throw new ArgumentOutOfRangeException("count");
                if (salt == null || salt.Length > 256)
                    throw new ArgumentException("GPU BIP39 密码的 UTF-8 长度不能超过 256 字节。");
                if (string.IsNullOrEmpty(target) || target.Length > 63)
                    throw new ArgumentException("SOL 地址格式无效。");
                int unknownCount = fixedIndices.Count(value => value < 0);
                if (unknownCount > candidateIndices.Length)
                    throw new ArgumentException("空位数量超过候选词数量。");
                token.ThrowIfCancellationRequested();

                Write(queue, wordListBuffer, wordList);
                byte[] fixedBytes = Integers(fixedIndices);
                try { Write(queue, fixedBuffer, fixedBytes); }
                finally { Array.Clear(fixedBytes, 0, fixedBytes.Length); }
                if (candidateIndices.Length > 0)
                {
                    byte[] candidateBytes = Integers(candidateIndices);
                    try { Write(queue, candidateBuffer, candidateBytes); }
                    finally { Array.Clear(candidateBytes, 0, candidateBytes.Length); }
                }
                var rankBytes = new byte[24];
                byte[] rankValue = startRank.ToByteArray();
                Array.Copy(rankValue, rankBytes, Math.Min(rankValue.Length, rankBytes.Length));
                try { Write(queue, startRankBuffer, rankBytes); }
                finally { Array.Clear(rankBytes, 0, rankBytes.Length); Array.Clear(rankValue, 0, rankValue.Length); }
                int candidateCount = candidateIndices.Length;
                Check(SetIntArg(generateKernel, 4, (UIntPtr)4, ref candidateCount), "恢复候选词数量参数");
                Check(SetIntArg(generateKernel, 5, (UIntPtr)4, ref unknownCount), "恢复空位数量参数");
                Check(SetIntArg(generateKernel, 6, (UIntPtr)4, ref count), "恢复组合批次参数");
                Check(clEnqueueNDRangeKernel(queue, generateKernel, 1, null,
                    new[] { (UIntPtr)count }, null, 0, IntPtr.Zero, IntPtr.Zero),
                    "恢复候选词组合与校验");

                int saltLength = salt.Length;
                Write(queue, saltBuffer, salt);
                Check(SetIntArg(deriveKernel, 2, (UIntPtr)4, ref saltLength), "恢复密码盐长度参数");
                int iterations = 2048;
                Check(SetIntArg(deriveKernel, 3, (UIntPtr)4, ref iterations), "恢复迭代次数参数");
                Check(SetIntArg(deriveKernel, 5, (UIntPtr)4, ref count), "恢复种子批次参数");
                Check(clEnqueueNDRangeKernel(queue, deriveKernel, 1, null,
                    new[] { (UIntPtr)count }, null, 0, IntPtr.Zero, IntPtr.Zero),
                    "恢复 BIP39 种子");

                int found = RunMatch(target, count, 64, token);
                var flags = new byte[checked(count * 4)];
                Check(clEnqueueReadBuffer(queue, validBuffer, 1, UIntPtr.Zero,
                    (UIntPtr)flags.Length, flags, 0, IntPtr.Zero, IntPtr.Zero),
                    "恢复校验计数读取");
                int checksumValid = 0;
                for (int i = 0; i < count; i++)
                    if (BitConverter.ToInt32(flags, i * 4) != 0) checksumValid++;
                string phrase = null;
                if (found >= 0)
                {
                    var phraseBytes = new byte[128];
                    try
                    {
                        Check(clEnqueueReadBuffer(queue, phraseBuffer, 1,
                            (UIntPtr)(found * 128), (UIntPtr)128, phraseBytes,
                            0, IntPtr.Zero, IntPtr.Zero), "恢复命中助记词读取");
                        int length = Array.IndexOf(phraseBytes, (byte)0);
                        if (length < 0) length = phraseBytes.Length;
                        phrase = Encoding.ASCII.GetString(phraseBytes, 0, length);
                    }
                    finally { Array.Clear(phraseBytes, 0, phraseBytes.Length); }
                }
                return new GeneratedSearchResult {
                    Index = found, ChecksumValid = checksumValid, Phrase = phrase
                };
            }

            private static byte[] Integers(int[] values)
            {
                var bytes = new byte[values.Length * 4];
                for (int i = 0; i < values.Length; i++)
                    System.Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, bytes, i * 4, 4);
                return bytes;
            }

            private void WriteValidFlags(int count, bool valid)
            {
                var flags = new byte[checked(count * 4)];
                if (valid)
                    for (int i = 0; i < count; i++)
                        System.Buffer.BlockCopy(BitConverter.GetBytes(1), 0, flags, i * 4, 4);
                Write(queue, validBuffer, flags);
            }

            public byte[] DeriveSeeds(byte[] packedPhrases, int count, byte[] salt,
                CancellationToken token)
            {
                return DeriveSeeds(packedPhrases, count, salt, 2048, token);
            }

            public byte[] DeriveSeeds(byte[] packedPhrases, int count, byte[] salt,
                int iterations, CancellationToken token)
            {
                WriteValidFlags(count, true);
                LaunchDerive(packedPhrases, count, salt, iterations, token);
                var seeds = new byte[checked(count * 64)];
                try
                {
                    Check(clEnqueueReadBuffer(queue, seedsBuffer, 1, UIntPtr.Zero,
                        (UIntPtr)seeds.Length, seeds, 0, IntPtr.Zero, IntPtr.Zero), "恢复种子读取");
                    token.ThrowIfCancellationRequested();
                    return seeds; // Ownership transfers to the caller, which clears the array.
                }
                catch { Array.Clear(seeds, 0, seeds.Length); throw; }
            }

            private void LaunchDerive(byte[] packedPhrases, int count, byte[] salt,
                CancellationToken token)
            {
                LaunchDerive(packedPhrases, count, salt, 2048, token);
            }

            private void LaunchDerive(byte[] packedPhrases, int count, byte[] salt,
                int iterations, CancellationToken token)
            {
                if (disposed) throw new ObjectDisposedException("SolanaAddressMatcher");
                if (count < 1 || count > batchCapacity) throw new ArgumentOutOfRangeException("count");
                if (packedPhrases == null || packedPhrases.Length < count * 128)
                    throw new ArgumentException("助记词缓冲区不足。");
                if (salt == null || salt.Length > 256)
                    throw new ArgumentException("GPU BIP39 密码的 UTF-8 长度不能超过 256 字节。");
                token.ThrowIfCancellationRequested();
                Write(queue, phraseBuffer, packedPhrases, count * 128);
                Write(queue, saltBuffer, salt);
                int saltLength = salt.Length;
                Check(SetIntArg(deriveKernel, 2, (UIntPtr)4, ref saltLength), "恢复密码盐长度参数");
                Check(SetIntArg(deriveKernel, 3, (UIntPtr)4, ref iterations), "恢复迭代次数参数");
                Check(SetIntArg(deriveKernel, 5, (UIntPtr)4, ref count), "恢复种子批次参数");
                Check(clEnqueueNDRangeKernel(queue, deriveKernel, 1, null,
                    new[] { (UIntPtr)count }, null, 0, IntPtr.Zero, IntPtr.Zero), "恢复 BIP39 种子");
            }

            private int RunMatch(string target, int count, int stride, CancellationToken token)
            {
                var targetBytes = new byte[64];
                Encoding.ASCII.GetBytes(target).CopyTo(targetBytes, 0);
                Write(queue, targetBuffer, targetBytes);
                var zero = new byte[4];
                Write(queue, outputBuffer, zero);
                int targetLength = target.Length;
                Check(SetIntArg(matchKernel, 2, (UIntPtr)4, ref targetLength), "恢复地址长度参数");
                Check(SetIntArg(matchKernel, 3, (UIntPtr)4, ref count), "恢复批次参数");
                Check(SetIntArg(matchKernel, 4, (UIntPtr)4, ref stride), "恢复种子步长参数");
                Check(clEnqueueNDRangeKernel(queue, matchKernel, 1, null,
                    new[] { (UIntPtr)count }, null, 0, IntPtr.Zero, IntPtr.Zero), "恢复搜索");
                var output = new byte[4];
                Check(clEnqueueReadBuffer(queue, outputBuffer, 1, UIntPtr.Zero, (UIntPtr)output.Length,
                    output, 0, IntPtr.Zero, IntPtr.Zero), "恢复结果读取");
                token.ThrowIfCancellationRequested();
                uint found = BitConverter.ToUInt32(output, 0);
                return found == 0 ? -1 : checked((int)found - 1);
            }

            private void WipeBuffer(IntPtr buffer, int length)
            {
                if (buffer == IntPtr.Zero || queue == IntPtr.Zero) return;
                // Blocking writes on this in-order queue also wait for prior kernels.
                var zeros = new byte[Math.Min(length, 65536)];
                for (int offset = 0; offset < length; offset += zeros.Length)
                {
                    int size = Math.Min(zeros.Length, length - offset);
                    if (clEnqueueWriteBuffer(queue, buffer, 1, (UIntPtr)offset, (UIntPtr)size,
                        zeros, 0, IntPtr.Zero, IntPtr.Zero) != 0) break;
                }
            }

            internal void ClearSecrets()
            {
                // Driver failure can prevent erasure; continue releasing every resource.
                var buffers = new[] { outputBuffer, targetBuffer, validBuffer, seedsBuffer,
                    saltBuffer, phraseBuffer, startRankBuffer, candidateBuffer, fixedBuffer, wordListBuffer };
                var lengths = new[] { 4, 64, checked(batchCapacity * 4), checked(batchCapacity * 64),
                    256, checked(batchCapacity * 128), 24, 2048 * 4, 64, 2048 * 16 };
                for (int i = 0; i < buffers.Length; i++)
                    try { WipeBuffer(buffers[i], lengths[i]); } catch { }

            }

            private void ReleaseResources()
            {
                ClearSecrets();
                if (outputBuffer != IntPtr.Zero) clReleaseMemObject(outputBuffer);
                if (targetBuffer != IntPtr.Zero) clReleaseMemObject(targetBuffer);
                if (validBuffer != IntPtr.Zero) clReleaseMemObject(validBuffer);
                if (seedsBuffer != IntPtr.Zero) clReleaseMemObject(seedsBuffer);
                if (saltBuffer != IntPtr.Zero) clReleaseMemObject(saltBuffer);
                if (phraseBuffer != IntPtr.Zero) clReleaseMemObject(phraseBuffer);
                if (startRankBuffer != IntPtr.Zero) clReleaseMemObject(startRankBuffer);
                if (candidateBuffer != IntPtr.Zero) clReleaseMemObject(candidateBuffer);
                if (fixedBuffer != IntPtr.Zero) clReleaseMemObject(fixedBuffer);
                if (wordListBuffer != IntPtr.Zero) clReleaseMemObject(wordListBuffer);
                if (generateKernel != IntPtr.Zero) clReleaseKernel(generateKernel);
                if (matchKernel != IntPtr.Zero) clReleaseKernel(matchKernel);
                if (deriveKernel != IntPtr.Zero) clReleaseKernel(deriveKernel);
                if (program != IntPtr.Zero) clReleaseProgram(program);
                if (queue != IntPtr.Zero) clReleaseCommandQueue(queue);
                if (context != IntPtr.Zero) clReleaseContext(context);
                outputBuffer = targetBuffer = validBuffer = seedsBuffer = saltBuffer =
                    phraseBuffer = startRankBuffer = candidateBuffer = fixedBuffer =
                    wordListBuffer = generateKernel = matchKernel = deriveKernel =
                    program = queue = context = IntPtr.Zero;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                ReleaseResources();
                EngineLock.Release();
            }
        }
    }
}
