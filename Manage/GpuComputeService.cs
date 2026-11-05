using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace jmsTools.Manage
{
    public sealed class GpuComputeSettings
    {
        public const int MaximumBatchSize = 16777216;
        public int Version { get; set; } = 1;
        public bool Enabled { get; set; } = true;
        public string Backend { get; set; } = "OpenCL";
        public string DeviceKey { get; set; } = "";
        public int BatchSize { get; set; } = 1048576;

        public void Validate()
        {
            if (Version != 1 || Backend != "OpenCL") throw new ArgumentException("GPU 配置版本或计算后端不支持。");
            if (Enabled && string.IsNullOrWhiteSpace(DeviceKey)) throw new ArgumentException("请先选择 GPU 设备。");
            if (BatchSize < 256 || BatchSize > MaximumBatchSize) throw new ArgumentException("每批元素数需在 256 至 16,777,216 之间。");
        }

        public static string ConfigurationPath => Path.Combine(AppPaths.DataDirectory, "GpuCompute.json");
        public static GpuComputeSettings Load()
        {
            var settings = JsonConvert.DeserializeObject<GpuComputeSettings>(File.ReadAllText(ConfigurationPath));
            if (settings == null) throw new ArgumentException("GPU 配置为空。");
            settings.Validate();
            return settings;
        }

        public void Save()
        {
            Validate();
            var temporary = ConfigurationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(ConfigurationPath)) File.Replace(temporary, ConfigurationPath, null);
                else File.Move(temporary, ConfigurationPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public sealed class GpuComputeDevice
    {
        public string Key { get; internal set; }
        public string Name { get; internal set; }
        public string Vendor { get; internal set; }
        public string Driver { get; internal set; }
        public ulong MemoryBytes { get; internal set; }
        public uint ComputeUnits { get; internal set; }
        public bool Available { get; internal set; }
        internal IntPtr Handle;
        public override string ToString() => Name;
    }

    // 通用数组计算接口，独立于钱包、网络查询和靓号引擎。
    public static class GpuComputeService
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private const string Library = "OpenCL.dll";
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetPlatformIDs(uint count, [Out] IntPtr[] platforms, out uint actual);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clGetPlatformInfo(IntPtr platform, uint name, UIntPtr size, byte[] value, out UIntPtr actual);
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
        [DllImport(Library, CharSet = CharSet.Ansi)] private static extern IntPtr clCreateKernel(IntPtr program, string name, out int error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern IntPtr clCreateBuffer(IntPtr context, ulong flags, UIntPtr size, IntPtr host, out int error);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clSetKernelArg(IntPtr kernel, uint index, UIntPtr size, ref IntPtr value);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clEnqueueWriteBuffer(IntPtr queue, IntPtr buffer, uint blocking, UIntPtr offset, UIntPtr size, byte[] data, uint count, IntPtr waits, IntPtr ev);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clEnqueueReadBuffer(IntPtr queue, IntPtr buffer, uint blocking, UIntPtr offset, UIntPtr size, byte[] data, uint count, IntPtr waits, IntPtr ev);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clEnqueueNDRangeKernel(IntPtr queue, IntPtr kernel, uint dims, UIntPtr[] offset, UIntPtr[] global, UIntPtr[] local, uint count, IntPtr waits, IntPtr ev);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(Library)] private static extern int clFinish(IntPtr queue);
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

        private static void Check(int error, string operation)
        {
            if (error != 0) throw new InvalidOperationException(operation + "失败（OpenCL " + error + "）。");
        }
        private static byte[] Info(IntPtr device, uint field)
        {
            UIntPtr length;
            Check(clGetDeviceInfo(device, field, UIntPtr.Zero, null, out length), "读取 GPU 信息");
            var bytes = new byte[checked((int)length.ToUInt64())];
            Check(clGetDeviceInfo(device, field, (UIntPtr)bytes.Length, bytes, out length), "读取 GPU 信息");
            return bytes;
        }
        private static string Text(IntPtr device, uint field) => Encoding.UTF8.GetString(Info(device, field)).TrimEnd('\0');

        public static GpuComputeDevice[] EnumerateDevices()
        {
            uint count;
            var code = clGetPlatformIDs(0, null, out count);
            if (code == -1001) return new GpuComputeDevice[0];
            Check(code, "检测 OpenCL 平台");
            if (count == 0) return new GpuComputeDevice[0];
            var platforms = new IntPtr[count];
            Check(clGetPlatformIDs(count, platforms, out count), "检测 OpenCL 平台");
            var result = new List<GpuComputeDevice>();
            foreach (var platform in platforms)
            {
                uint deviceCount;
                code = clGetDeviceIDs(platform, 4, 0, null, out deviceCount);
                if (code == -1) continue;
                Check(code, "检测 GPU");
                if (deviceCount == 0) continue;
                var devices = new IntPtr[deviceCount];
                Check(clGetDeviceIDs(platform, 4, deviceCount, devices, out deviceCount), "检测 GPU");
                UIntPtr size;
                Check(clGetPlatformInfo(platform, 0x0902, UIntPtr.Zero, null, out size), "读取平台名称");
                var platformBytes = new byte[checked((int)size.ToUInt64())];
                Check(clGetPlatformInfo(platform, 0x0902, size, platformBytes, out size), "读取平台名称");
                var platformName = Encoding.UTF8.GetString(platformBytes).TrimEnd('\0');
                for (int i = 0; i < devices.Length; i++)
                {
                    var device = devices[i];
                    var name = Text(device, 0x102B);
                    var vendor = Text(device, 0x102C);
                    result.Add(new GpuComputeDevice {
                        Key = platformName + "|" + vendor + "|" + name + "|" + i,
                        Name = name, Vendor = vendor, Driver = Text(device, 0x102D), Handle = device,
                        MemoryBytes = BitConverter.ToUInt64(Info(device, 0x101F), 0),
                        ComputeUnits = BitConverter.ToUInt32(Info(device, 0x1002), 0),
                        Available = BitConverter.ToUInt32(Info(device, 0x1027), 0) != 0 &&
                            BitConverter.ToUInt32(Info(device, 0x1028), 0) != 0
                    });
                }
            }
            return result.ToArray();
        }

        public static Task<float[]> AddVectorsAsync(float[] left, float[] right, GpuComputeSettings settings, CancellationToken token)
        {
            if (left == null || right == null || left.Length != right.Length) throw new ArgumentException("两组数组必须非空且长度相同。");
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            if (!settings.Enabled) throw new InvalidOperationException("通用 GPU 计算已禁用。");
            token.ThrowIfCancellationRequested();
            // 获取独立快照，调用方后续修改输入或配置不会影响已提交任务。
            var snapshot = new GpuComputeSettings { Enabled = true, DeviceKey = settings.DeviceKey, BatchSize = settings.BatchSize };
            return ComputeAsync((float[])left.Clone(), (float[])right.Clone(), snapshot, token);
        }

        private static async Task<float[]> ComputeAsync(float[] left, float[] right, GpuComputeSettings settings, CancellationToken token)
        {
            await Gate.WaitAsync(token).ConfigureAwait(false);
            try { return await Task.Run(() => Compute(left, right, settings, token), token).ConfigureAwait(false); }
            finally { Gate.Release(); }
        }

        private static float[] Compute(float[] left, float[] right, GpuComputeSettings settings, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var device = EnumerateDevices().SingleOrDefault(item => item.Key == settings.DeviceKey);
            if (device == null || !device.Available) throw new InvalidOperationException("所选 GPU 不可用，请重新检测并选择设备。");
            var result = new float[left.Length];
            if (left.Length == 0) return result;
            IntPtr context = IntPtr.Zero, queue = IntPtr.Zero, program = IntPtr.Zero, kernel = IntPtr.Zero;
            var buffers = new IntPtr[3];
            try
            {
                int error;
                context = clCreateContext(IntPtr.Zero, 1, new[] { device.Handle }, IntPtr.Zero, IntPtr.Zero, out error); Check(error, "创建计算上下文");
                queue = clCreateCommandQueue(context, device.Handle, 0, out error); Check(error, "创建计算队列");
                program = clCreateProgramWithSource(context, 1, new[] {
                    "__kernel void vector_add(__global const float* a, __global const float* b, __global float* c) { size_t i=get_global_id(0); c[i]=a[i]+b[i]; }"
                }, null, out error); Check(error, "创建计算程序");
                Check(clBuildProgram(program, 1, new[] { device.Handle }, "", IntPtr.Zero, IntPtr.Zero), "编译向量计算程序");
                kernel = clCreateKernel(program, "vector_add", out error); Check(error, "创建计算内核");
                int capacity = Math.Min(settings.BatchSize, left.Length);
                int bytes = checked(capacity * sizeof(float));
                for (int i = 0; i < buffers.Length; i++)
                {
                    buffers[i] = clCreateBuffer(context, 1, (UIntPtr)bytes, IntPtr.Zero, out error); Check(error, "分配计算缓冲区");
                    Check(clSetKernelArg(kernel, (uint)i, (UIntPtr)IntPtr.Size, ref buffers[i]), "设置计算参数");
                }
                var a = new byte[bytes]; var b = new byte[bytes]; var c = new byte[bytes];
                for (int offset = 0; offset < left.Length; offset += capacity)
                {
                    token.ThrowIfCancellationRequested();
                    int count = Math.Min(capacity, left.Length - offset);
                    int byteCount = count * sizeof(float);
                    Buffer.BlockCopy(left, offset * sizeof(float), a, 0, byteCount);
                    Buffer.BlockCopy(right, offset * sizeof(float), b, 0, byteCount);
                    Check(clEnqueueWriteBuffer(queue, buffers[0], 1, UIntPtr.Zero, (UIntPtr)byteCount, a, 0, IntPtr.Zero, IntPtr.Zero), "上传数组 A");
                    Check(clEnqueueWriteBuffer(queue, buffers[1], 1, UIntPtr.Zero, (UIntPtr)byteCount, b, 0, IntPtr.Zero, IntPtr.Zero), "上传数组 B");
                    Check(clEnqueueNDRangeKernel(queue, kernel, 1, null, new[] { (UIntPtr)count }, null, 0, IntPtr.Zero, IntPtr.Zero), "执行 GPU 计算");
                    Check(clEnqueueReadBuffer(queue, buffers[2], 1, UIntPtr.Zero, (UIntPtr)byteCount, c, 0, IntPtr.Zero, IntPtr.Zero), "读取计算结果");
                    Buffer.BlockCopy(c, 0, result, offset * sizeof(float), byteCount);
                }
                token.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                // 等待已提交的短批次完成后再释放资源；取消在批次边界生效。
                if (queue != IntPtr.Zero) clFinish(queue);
                foreach (var buffer in buffers) if (buffer != IntPtr.Zero) clReleaseMemObject(buffer);
                if (kernel != IntPtr.Zero) clReleaseKernel(kernel);
                if (program != IntPtr.Zero) clReleaseProgram(program);
                if (queue != IntPtr.Zero) clReleaseCommandQueue(queue);
                if (context != IntPtr.Zero) clReleaseContext(context);
            }
        }

        public static async Task<double> SelfTestAsync(GpuComputeSettings settings, CancellationToken token)
        {
            var a = Enumerable.Range(0, 65539).Select(i => (i % 257 - 128) * 0.25f).ToArray();
            var b = Enumerable.Range(0, a.Length).Select(i => (i % 127 - 63) * 0.5f).ToArray();
            var watch = Stopwatch.StartNew();
            var result = await AddVectorsAsync(a, b, settings, token).ConfigureAwait(false);
            for (int i = 0; i < result.Length; i++)
                if (float.IsNaN(result[i]) || result[i] != a[i] + b[i]) throw new InvalidOperationException("GPU 计算结果校验失败。");
            return watch.Elapsed.TotalMilliseconds;
        }
    }
}
