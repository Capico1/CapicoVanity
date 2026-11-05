using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace jmsTools.Manage
{
    internal sealed class VanityGpuSettings
    {
        public static readonly int[] SolChoices = { 16384, 65536, 262144, 1048576, 0 };
        // CUDA 固定线程块为 128；只提供使 255 * 倍率完整对齐线程块的档位。
        public static readonly uint[] EvmChoices = { 0, 128, 256, 1024, 4096, 8192, 16384 };
        public int SolBatchSize { get; set; } = 0;
        public uint EvmBatchMultiple { get; set; } = 0;
        public static string FilePath => Path.Combine(AppPaths.DataDirectory, "VanityGpu.json");
        public static void ValidateSol(int value)
        {
            if (!SolChoices.Contains(value)) throw new ArgumentException("SOL GPU 批次不在可选范围内。");
        }
        public static void ValidateEvm(uint value)
        {
            if (!EvmChoices.Contains(value)) throw new ArgumentException("EVM GPU 批次倍率不在可选范围内。");
        }
        public void Validate() { ValidateSol(SolBatchSize); ValidateEvm(EvmBatchMultiple); }
        public static VanityGpuSettings Load()
        {
            if (!File.Exists(FilePath)) return new VanityGpuSettings();
            var value = JsonConvert.DeserializeObject<VanityGpuSettings>(File.ReadAllText(FilePath));
            if (value == null) throw new ArgumentException("靓号 GPU 配置为空。");
            value.Validate(); return value;
        }
        public void Save()
        {
            Validate();
            var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
