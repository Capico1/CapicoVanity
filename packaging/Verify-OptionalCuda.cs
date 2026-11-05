using System;
using System.IO;
using System.Reflection;
using System.Threading;

static class VerifyOptionalCuda
{
    static int Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(args[0]);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
                var file = Path.Combine(root, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };
            var assembly = Assembly.LoadFrom(Path.Combine(root, "jmsTools.exe"));
            var gpu = assembly.GetType("jmsTools.Manage.VanityGpuGenerator");
            var check = gpu.GetMethod("CheckAbi", BindingFlags.Static | BindingFlags.NonPublic);
            if (args.Length > 1 && args[1] == "--reject")
            {
                try { check.Invoke(null, null); throw new Exception("Modified native binary was accepted"); }
                catch (TargetInvocationException error)
                {
                    if (!(error.InnerException is InvalidOperationException) || !error.InnerException.Message.Contains("完整性")) throw;
                    Console.WriteLine("PASS: modified CUDA binary is rejected before native loading");
                }
                return 0;
            }
            check.Invoke(null, null);
            Console.WriteLine("PASS: pinned optional CUDA backend loads with restricted native search paths and valid ABI");
            object[] detect = { -1 };
            gpu.GetMethod("Detect").Invoke(null, detect);
            if ((int)detect[0] < 0) throw new Exception("No compatible CUDA device for this hardware check");
            using (var timeout = new CancellationTokenSource(30000))
            {
                var result = gpu.GetMethod("Generate").Invoke(null, new object[] {
                    "", "a", false, detect[0], timeout.Token,
                    new Action<long, ulong>((attempts, rate) => { }), new Action<string>(message => { }), (uint)128
                });
                string address = (string)result.GetType().GetField("Address").GetValue(result);
                string privateKey = (string)result.GetType().GetField("PrivateKey").GetValue(result);
                var signer = Assembly.LoadFrom(Path.Combine(root, "Nethereum.Signer.dll")).GetType("Nethereum.Signer.EthECKey");
                var key = Activator.CreateInstance(signer, new object[] { privateKey });
                string derived = (string)signer.GetMethod("GetPublicAddress", Type.EmptyTypes).Invoke(key, null);
                if (derived != address || !address.EndsWith("a", StringComparison.OrdinalIgnoreCase)) throw new Exception("CUDA result failed address verification");
                Console.WriteLine("PASS: optional CUDA search result matches the suffix and independently derived public address");
            }
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL: " + error); return 1; }
    }
}
