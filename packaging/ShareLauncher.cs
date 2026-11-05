using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Capico 靓号")]
[assembly: AssemblyProduct("Capico 靓号")]
[assembly: AssemblyCompany("Capico")]

internal static class ShareLauncher
{
    private static void RejectLinks(string path)
    {
        for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("程序目录不能使用符号链接或目录联接。");
    }

    private static string Target(string root, string name)
    {
        if (name.IndexOf(':') >= 0) throw new InvalidDataException("Invalid package path.");
        var path = Path.GetFullPath(Path.Combine(root, name));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid package path.");
        return path;
    }

    private static string Digest(Stream input)
    {
        using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(input));
    }

    // Locks remain open for the lifetime of the child, preventing modification of existing files.
    private static void Verify(ZipArchive archive, string root, List<FileStream> locks)
    {
        RejectLinks(root);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(item => item.Name.Length > 0))
        {
            var path = Target(root, entry.FullName);
            if (!expected.Add(path)) throw new InvalidDataException("Duplicate package entry.");
            RejectLinks(Path.GetDirectoryName(path));
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("程序文件缺失或被替换，请重新获取程序。个人钱包数据不受影响。");
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            locks.Add(file);
            using (var original = entry.Open())
                if (file.Length != entry.Length || Digest(file) != Digest(original))
                    throw new InvalidDataException("程序文件校验失败，请重新获取程序。个人钱包数据不受影响。");
        }
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var child in Directory.GetDirectories(directory))
            {
                RejectLinks(child);
                pending.Push(child);
            }
            foreach (var file in Directory.GetFiles(directory))
                if (!expected.Contains(Path.GetFullPath(file)))
                    throw new InvalidDataException("程序目录出现额外文件，已停止启动。请保留个人数据并重新获取程序。");
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var locks = new List<FileStream>();
        try
        {
            byte[] payload;
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("JmsTools.Payload.zip"))
            using (var buffer = new MemoryStream())
            {
                input.CopyTo(buffer);
                payload = buffer.ToArray();
            }
            string version;
            using (var input = new MemoryStream(payload))
                version = Digest(input).Replace("/", "_").Replace("+", "-").TrimEnd('=');
            var parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NanaVanity");
            var destination = Path.Combine(parent, "Programs", version);
            RejectLinks(destination);
            using (var memory = new MemoryStream(payload))
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Read))
            using (var gate = new Mutex(false, "Local\\NanaVanityShare-" + version))
            {
                try { gate.WaitOne(); } catch (AbandonedMutexException) { }
                try
                {
                    if (!Directory.Exists(destination))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        var staging = Path.Combine(parent, "Programs", "unpack-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(staging);
                        try
                        {
                            foreach (var entry in archive.Entries)
                            {
                                var target = Target(staging, entry.FullName);
                                if (entry.Name.Length == 0) { Directory.CreateDirectory(target); continue; }
                                Directory.CreateDirectory(Path.GetDirectoryName(target));
                                entry.ExtractToFile(target);
                            }
                            Directory.Move(staging, destination);
                        }
                        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                    }
                    // Never trust a disk manifest or a previous extraction marker.
                    Verify(archive, destination, locks);
                }
                finally { gate.ReleaseMutex(); }
            }
            if (args.Length == 1 && (args[0] == "--extract-only" || args[0] == "--verify-only")) return 0;
            var data = Path.Combine(parent, "Data");
            RejectLinks(data);
            Directory.CreateDirectory(data);
            foreach (var name in new[] { "Ethrpc_nodes.txt", "Bscrpc_nodes.txt", "Solrpc_nodes.txt" })
            {
                var target = Path.Combine(data, name);
                if (!File.Exists(target))
                    try { File.Copy(Path.Combine(destination, name), target, false); }
                    catch (IOException) { if (!File.Exists(target)) throw; }
            }
            var start = new ProcessStartInfo(Path.Combine(destination, "jmsTools.exe")) {
                WorkingDirectory = data, UseShellExecute = false
            };
            start.EnvironmentVariables["NANAVANITY_DATA_DIRECTORY"] = data;
            using (var process = Process.Start(start)) process.WaitForExit();
            return 0;
        }
        catch (Exception error)
        {
            if (!args.Contains("--verify-only"))
                MessageBox.Show("无法启动程序：" + error.Message, "Capico 靓号", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally { foreach (var file in locks) file.Dispose(); }
    }
}
