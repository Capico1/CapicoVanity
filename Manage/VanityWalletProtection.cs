using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace jmsTools.Manage
{
    internal static class VanityWalletProtection
    {
        private const string Scheme = "windows-dpapi-current-user-v1";
        internal const string Evm = "EVM (ETH / BSC)";

        public static JObject Read(string path)
        {
            var file = new FileInfo(path);
            if (file.Length > 128 * 1024 || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("不支持的钱包文件。");
            return JObject.Parse(File.ReadAllText(path));
        }

        public static bool IsProtected(JObject record) => (string)record["keyProtection"] == Scheme &&
            !string.IsNullOrWhiteSpace((string)record["encryptedPrivateKey"]) &&
            record.Property("privateKey") == null && record.Property("secretKey") == null;

        private static byte[] Context(JObject record)
        {
            string network = (string)record["network"], address = (string)record["address"];
            if ((network != "SOL" && network != Evm) || string.IsNullOrWhiteSpace(address) || address.Length > 128)
                throw new InvalidDataException("钱包地址或网络无效。");
            return Encoding.UTF8.GetBytes("JmsTools.VanityWallet/v1\n" + network + "\n" + address);
        }

        public static void Protect(JObject record, string privateKey)
        {
            if (string.IsNullOrWhiteSpace(privateKey) || privateKey.Length > 256)
                throw new InvalidDataException("私钥格式无效。");
            var plaintext = Encoding.UTF8.GetBytes(privateKey);
            try
            {
                var ciphertext = ProtectedData.Protect(plaintext, Context(record), DataProtectionScope.CurrentUser);
                record.Remove("privateKey"); record.Remove("secretKey");
                record["keyProtection"] = Scheme;
                record["encryptedPrivateKey"] = Convert.ToBase64String(ciphertext);
                if (!string.Equals(Decrypt(record), privateKey, StringComparison.Ordinal))
                    throw new CryptographicException("钱包加密复核失败。");
            }
            finally { Array.Clear(plaintext, 0, plaintext.Length); }
        }

        public static string Decrypt(JObject record)
        {
            if (!IsProtected(record)) throw new InvalidDataException("钱包没有可用的加密私钥。");
            var plaintext = ProtectedData.Unprotect(Convert.FromBase64String((string)record["encryptedPrivateKey"]), Context(record), DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(plaintext); }
            finally { Array.Clear(plaintext, 0, plaintext.Length); }
        }

        public static void Write(string path, JObject record, bool replace = false)
        {
            if (!IsProtected(record)) throw new InvalidDataException("禁止保存明文钱包。");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes(record.ToString(Newtonsoft.Json.Formatting.Indented));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (replace) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { try { File.Delete(temporary); } catch { } }
        }

        public static void Migrate(string directory, out int migrated, out int failed)
        {
            migrated = failed = 0;
            foreach (string folder in new[] { directory, Path.Combine(directory, "Deleted") })
            {
                if (!Directory.Exists(folder)) continue;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { failed++; continue; }
                foreach (string path in Directory.EnumerateFiles(folder, "vanity-*.json"))
                {
                    try
                    {
                        var record = Read(path);
                        if (IsProtected(record)) continue;
                        if (record.Property("keyProtection") != null || record.Property("encryptedPrivateKey") != null)
                            throw new InvalidDataException("未知钱包加密格式。");
                        string key = (string)record["privateKey"], address = (string)record["address"];
                        Context(record);
                        if (string.IsNullOrWhiteSpace(key) || key.Length > 256) throw new InvalidDataException("私钥无效。");
                        if ((string)record["network"] == "SOL")
                            SolanaVanityGenerator.VerifyResult(new VanityGpuGenerator.Result { Address = address, PrivateKey = key });
                        else if (!string.Equals(new Nethereum.Signer.EthECKey(key).GetPublicAddress(), address, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("私钥和地址不匹配。");
                        Protect(record, key);
                        Write(path, record, true);
                        migrated++;
                    }
                    catch { failed++; }
                }
            }
        }
    }
}
