using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

static class VerifyRecoveryBuffers
{
    static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static int Main(string[] args)
    {
        byte[] phrases = null, seeds = null;
        try
        {
            var root = Path.GetFullPath(args[0]);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
                var file = Path.Combine(root, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };
            var assembly = Assembly.LoadFrom(Path.Combine(root, "jmsTools.exe"));
            var gpu = assembly.GetType("jmsTools.Manage.SolanaVanityGpuGenerator");
            object[] detect = { IntPtr.Zero };
            gpu.GetMethod("Detect", Static).Invoke(null, detect);
            Check((IntPtr)detect[0] != IntPtr.Zero, "Actual OpenCL GPU is available for recovery-buffer checks");
            var matcherType = gpu.GetNestedType("AddressMatcher", BindingFlags.NonPublic);
            using (var matcher = (IDisposable)Activator.CreateInstance(matcherType, Instance, null, new object[] { detect[0], 4 }, null))
            {
                const string phrase = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
                phrases = new byte[128];
                Encoding.UTF8.GetBytes(phrase).CopyTo(phrases, 0);
                var salt = Encoding.UTF8.GetBytes("mnemonic");
                var derive = matcherType.GetMethod("DeriveSeeds", Instance, null,
                    new[] { typeof(byte[]), typeof(int), typeof(byte[]), typeof(CancellationToken) }, null);
                seeds = (byte[])derive.Invoke(matcher, new object[] { phrases, 1, salt, CancellationToken.None });
                using (var expected = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(phrase), salt, 2048, HashAlgorithmName.SHA512))
                    Check(seeds.SequenceEqual(expected.GetBytes(64)), "GPU recovery seed agrees with .NET PBKDF2-SHA512 test vector");
                var queue = (IntPtr)matcherType.GetField("queue", Instance).GetValue(matcher);
                var write = gpu.GetMethod("Write", Static, null, new[] { typeof(IntPtr), typeof(IntPtr), typeof(byte[]) }, null);
                var read = gpu.GetMethod("clEnqueueReadBuffer", Static);
                var names = new[] { "phraseBuffer", "saltBuffer", "seedsBuffer", "fixedBuffer", "candidateBuffer", "startRankBuffer" };
                var lengths = new[] { 4 * 128, 256, 4 * 64, 64, 2048 * 4, 24 };
                for (int i = 0; i < names.Length; i++)
                {
                    var buffer = (IntPtr)matcherType.GetField(names[i], Instance).GetValue(matcher);
                    write.Invoke(null, new object[] { queue, buffer, Enumerable.Repeat((byte)0xA5, lengths[i]).ToArray() });
                }
                matcherType.GetMethod("ClearSecrets", Instance).Invoke(matcher, null);
                for (int i = 0; i < names.Length; i++)
                {
                    var buffer = (IntPtr)matcherType.GetField(names[i], Instance).GetValue(matcher);
                    var bytes = new byte[lengths[i]];
                    int code = (int)read.Invoke(null, new object[] { queue, buffer, (uint)1, UIntPtr.Zero, (UIntPtr)bytes.Length,
                        bytes, (uint)0, IntPtr.Zero, IntPtr.Zero });
                    Check(code == 0 && bytes.All(value => value == 0), "GPU buffer cleared before release: " + names[i]);
                }
                try { derive.Invoke(matcher, new object[] { phrases, 1, salt, new CancellationToken(true) }); throw new Exception("Canceled recovery did not stop"); }
                catch (TargetInvocationException e) { Check(e.InnerException is OperationCanceledException, "Canceled GPU recovery exits through disposal scope"); }
            }
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FAIL: " + e); return 1; }
        finally {
            if (phrases != null) Array.Clear(phrases, 0, phrases.Length);
            if (seeds != null) Array.Clear(seeds, 0, seeds.Length);
        }
    }
}
