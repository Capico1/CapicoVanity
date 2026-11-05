using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class VerifyShare
{
    private static void Check(bool passed, string label)
    {
        if (!passed) throw new Exception(label);
        Console.WriteLine("PASS: " + label);
    }

    private static System.Collections.Generic.IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in AllControls(child)) yield return nested;
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "NanaVanity-test-" + Guid.NewGuid().ToString("N"));
        var previousData = Environment.GetEnvironmentVariable("NANAVANITY_DATA_DIRECTORY");
        try
        {
            Directory.CreateDirectory(testRoot);
            Environment.SetEnvironmentVariable("NANAVANITY_DATA_DIRECTORY", testRoot);
            var appRoot = Path.GetFullPath(args[0]);
            Environment.CurrentDirectory = appRoot;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, request) => {
                var file = Path.Combine(appRoot, new AssemblyName(request.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var assembly = Assembly.LoadFrom(Path.Combine(appRoot, "jmsTools.exe"));
            using (var form = (Form)assembly.CreateInstance("jmsTools.Form1"))
            {
                form.Show();
                Application.DoEvents();
                Check(form.IsHandleCreated, "Main window opens with packaged dependencies");
                var controls = AllControls(form).ToArray();
                Check(!controls.Any(control => control.Text.Contains("币安")), "Binance UI is absent");
                Check(!assembly.GetType("jmsTools.Manage.BlockManage").GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Any(method => method.Name.IndexOf("Binance", StringComparison.OrdinalIgnoreCase) >= 0) &&
                    assembly.GetType("jmsTools.Manage.BinanceBalanceResult") == null, "Binance API backend is absent from the binary");
                Check(!controls.OfType<TabPage>().Any(tab => tab.Text == "测试器") &&
                    assembly.GetType("jmsTools.Manage.TesterGpuGenerator") == null &&
                    assembly.GetType("jmsTools.Manage.TesterMnemonicGpuBatch") == null, "Tester UI and backends are absent");
                Check(controls.OfType<TabPage>().Any(tab => tab.Text == "单地址查询"), "Single-address query is available");
                var recoveryTab = controls.OfType<TabPage>().Single(tab => tab.Name == "walletRecoveryTab");
                var hitsTab = controls.OfType<TabPage>().Single(tab => tab.Name == "foundTab");
                var tabs = (TabControl)recoveryTab.Parent;
                Check(tabs.SelectedTab.Name == "tabPage3" && tabs.TabPages[1].Name == "savedVanityTab",
                    "Vanity generation opens first with saved wallets next to it");
                tabs.SelectedTab = recoveryTab;
                Application.DoEvents();
                Check(recoveryTab.Controls.Find("recoveryStart", true).Single().Visible &&
                    hitsTab.Controls.Find("recoveryStart", true).Length == 0 &&
                    hitsTab.Controls.Find("openMnemonicRecovery", true).Length == 0, "Recovery controls belong to their own tab");
                ((Button)recoveryTab.Controls.Find("recoveryViewResults", true).Single()).PerformClick();
                Check(tabs.SelectedTab == hitsTab, "Recovery results button opens the hit list");
                var phraseInput = (TextBox)recoveryTab.Controls.Find("recoveryPhraseMask", true).Single();
                var showPhrase = (CheckBox)recoveryTab.Controls.Find("recoveryShowPhrase", true).Single();
                Check(phraseInput.UseSystemPasswordChar && !showPhrase.Checked, "Recovery mnemonic input is hidden by default");
                showPhrase.Checked = true;
                Check(!phraseInput.UseSystemPasswordChar, "Explicit reveal switch shows mnemonic input");
                var recoveryControl = phraseInput.Parent;
                while (recoveryControl != null && recoveryControl.GetType().Name != "MnemonicRecoveryForm") recoveryControl = recoveryControl.Parent;
                recoveryControl.GetType().GetMethod("SetRunningState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recoveryControl, new object[] { true });
                Check(phraseInput.UseSystemPasswordChar && !showPhrase.Checked && !showPhrase.Enabled, "Starting recovery hides the mnemonic and disables reveal");
                recoveryControl.GetType().GetMethod("SetRunningState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recoveryControl, new object[] { false });
                VerifyRecoveryPrivacy(assembly, form, recoveryTab, hitsTab);
                Check(controls.OfType<TabControl>().Any(tabs => tabs.TabPages.Count >= 4), "Application tabs are available");
                Check(controls.OfType<ComboBox>().Any(box => box.Items.Contains("模板模式")), "Packaged template mode is available");
                Check(controls.OfType<ComboBox>().Any(box => box.Items.Count == 54 && box.Items.Cast<object>().Any(item => item.ToString().StartsWith("AAAAAAAAAAAA（"))), "Packaged dropdown includes all 54 templates and 12 repeated characters");
                Check(controls.OfType<ComboBox>().Any(box => box.Items.Contains("找到指定数量停止") && box.Items.Contains("持续生成（手动停止）")), "Packaged stop policies are available");
                Check(!File.Exists(Path.Combine(appRoot, "BinanceApi.txt")), "No API credentials loaded or written");
                Check(!Directory.Exists(Path.Combine(appRoot, "VanityWallets")) || !Directory.EnumerateFiles(Path.Combine(appRoot, "VanityWallets"), "*", SearchOption.AllDirectories).Any(), "No publisher wallet records bundled");
                if (args.Contains("--cpu"))
                {
                    tabs.SelectedTab = tabs.TabPages["tabPage3"];
                    Exception failure = null;
                    form.BeginInvoke(new Action(async () => {
                        try { await VerifyCpuFallbackAsync(assembly, form, recoveryTab, testRoot); }
                        catch (Exception error) { failure = error; }
                        finally { form.Close(); }
                    }));
                    Application.Run(form);
                    if (failure != null) throw failure;
                }
                if (args.Length > 1 && args[1] == "--sol")
                {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    ((ComboBox)form.GetType().GetField("vanityNetwork", flags).GetValue(form)).SelectedIndex = 1;
                    var batch = (ComboBox)form.GetType().GetField("vanityBatch", flags).GetValue(form);
                    Check(batch.SelectedItem.ToString() == "自动优化（推荐）", "Packaged SOL UI defaults to automatic optimization");
                    var gpu = assembly.GetType("jmsTools.Manage.SolanaVanityGpuGenerator");
                    object[] detect = { IntPtr.Zero };
                    gpu.GetMethod("Detect").Invoke(null, detect);
                    Check((IntPtr)detect[0] != IntPtr.Zero, "Packaged SOL engine detects actual GPU");
                    using (var timeout = new CancellationTokenSource(120000))
                    {
                        var result = gpu.GetMethod("Generate").Invoke(null, new object[] {
                            "", "A", true, detect[0], timeout.Token, new Action<long, ulong>((count, rate) => { }), 0, null, -1, null, null
                        });
                        assembly.GetType("jmsTools.Manage.SolanaVanityGenerator").GetMethod("VerifyResult").Invoke(null, new[] { result });
                        Check(result.GetType().GetField("Address").GetValue(result).ToString().EndsWith("A"), "Packaged SOL GPU generates a matching address and passes independent signature verification");
                        var templateType = assembly.GetType("jmsTools.Manage.VanityTemplate");
                        var template = templateType.GetMethod("Parse").Invoke(null, new object[] { "ABCA", true, true });
                        result = gpu.GetMethod("Generate").Invoke(null, new object[] {
                            "", "", true, detect[0], timeout.Token, new Action<long, ulong>((count, rate) => { }), 0, null, -1, template, null
                        });
                        assembly.GetType("jmsTools.Manage.SolanaVanityGenerator").GetMethod("VerifyResult").Invoke(null, new[] { result });
                        Check((bool)templateType.GetMethod("Matches").Invoke(template, new object[] { result.GetType().GetField("Address").GetValue(result), true, true }), "Packaged SOL custom template ABCA matches and passes signature verification");
                    }
                }
                if (!form.IsDisposed) form.Close();
                Application.DoEvents();
            }
            Check(!File.Exists(Path.Combine(appRoot, "BinanceApi.txt")), "Closing does not save Binance credentials");
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("FAIL: " + error);
            return 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("NANAVANITY_DATA_DIRECTORY", previousData);
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }

    private static async Task VerifyCpuFallbackAsync(Assembly assembly, Form form, TabPage recoveryTab, string appRoot)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = form.GetType();
        Func<string, object> field = name => type.GetField(name, flags).GetValue(form);
        var engine = (ComboBox)field("vanityEngine");
        var network = (ComboBox)field("vanityNetwork");
        var batch = (ComboBox)field("vanityBatch");
        var recoveryEngine = (ComboBox)recoveryTab.Controls.Find("recoveryEngine", true).Single();
        Check((string)engine.SelectedItem == "自动（GPU 优先）" && (string)recoveryEngine.SelectedItem == "自动（GPU 优先）",
            "Vanity and recovery default to automatic engine selection");
        Check(engine.Items.Contains("仅 CPU") && recoveryEngine.Items.Contains("仅 CPU"),
            "Both workflows offer explicit CPU mode");

        var files = Path.Combine(appRoot, "VanityWallets");
        type.GetField("vanityOutputDirectory", flags).SetValue(form, files);
        Func<Task> run = async () => {
            ((Button)field("generate_btn")).PerformClick();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while ((bool)field("vanityRunning") && watch.Elapsed.TotalSeconds < 40)
                await Task.Delay(15);
            if ((bool)field("vanityRunning"))
            {
                ((Button)field("stop_btn")).PerformClick();
                throw new Exception("CPU vanity task timed out");
            }
        };
        // An invalid detected CUDA device fails the automatic batch lookup before
        // any GPU search. This exercises the runtime fallback on any host.
        type.GetField("vanityGpuDevice", flags).SetValue(form, int.MaxValue);
        batch.SelectedIndex = 0;
        ((ComboBox)field("vanityPatternMode")).SelectedIndex = 0;
        ((TextBox)field("textBoxPrefix")).Clear();
        ((TextBox)field("textBoxSuffix")).Text = "a";
        ((ComboBox)field("vanityStopMode")).SelectedIndex = 1;
        ((NumericUpDown)field("vanityTargetCount")).Value = 2;
        await run();
        var evmFiles = Directory.Exists(files) ? Directory.GetFiles(files, "*.json") : new string[0];
        if (evmFiles.Length != 2)
            throw new Exception("Expected two CPU wallet files; run status: " + ((Control)field("labelResults")).Text);
        Check(evmFiles.Length == 2 && evmFiles.All(path => File.ReadAllText(path).Contains("\"engine\": \"CPU\"")) &&
            ((string)field("generatedAddress")).EndsWith("a", StringComparison.OrdinalIgnoreCase),
            "Unusable CUDA automatically falls back to EVM CPU, matches suffix, and saves the requested two wallets");

        type.GetField("vanitySolGpuDevice", flags).SetValue(form, IntPtr.Zero);
        network.SelectedIndex = 1;
        Check(!batch.Enabled && (string)engine.SelectedItem == "自动（GPU 优先）",
            "No OpenCL GPU keeps automatic mode available and disables GPU batch controls");
        ((ComboBox)field("vanityStopMode")).SelectedIndex = 0;
        ((ComboBox)field("vanityPatternMode")).SelectedIndex = 1;
        ((ComboBox)field("vanityTemplatePosition")).SelectedIndex = 1;
        ((ComboBox)field("vanityTemplate")).Text = "AAA";
        ((CheckBox)field("checkBox1")).Checked = true;
        await run();
        string newWallet = Directory.GetFiles(files, "*.json").Except(evmFiles).Single();
        string solAddress = (string)field("generatedAddress");
        var templateType = assembly.GetType("jmsTools.Manage.VanityTemplate");
        var template = templateType.GetMethod("Parse").Invoke(null, new object[] { "AAA", true, true });
        Check(File.ReadAllText(newWallet).Contains("\"engine\": \"CPU\"") &&
            (bool)templateType.GetMethod("Matches").Invoke(template, new object[] { solAddress, true, true }),
            "No OpenCL GPU automatically uses SOL CPU, matches the template, and saves the verified wallet");

        var request = assembly.CreateInstance("jmsTools.Manage.MnemonicRecoveryRequest");
        var requestType = request.GetType();
        var service = assembly.GetType("jmsTools.Manage.MnemonicRecoveryService");
        var search = service.GetMethod("SearchWithDevice", BindingFlags.Static | BindingFlags.NonPublic);
        const string evmAddress = "0x9858EfFD232B4033E47d90003D41EC34EcaEda94";
        const string phrase = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        var walletAssembly = Assembly.Load("Solana.Unity.Wallet");
        var walletType = walletAssembly.GetType("Solana.Unity.Wallet.Wallet");
        var wordListType = walletAssembly.GetType("Solana.Unity.Wallet.Bip39.WordList");
        var seedModeType = walletAssembly.GetType("Solana.Unity.Wallet.SeedMode");
        var english = wordListType.IsEnum ? Enum.Parse(wordListType, "English")
            : wordListType.GetProperty("English")?.GetValue(null) ?? wordListType.GetField("English").GetValue(null);
        var solWallet = Activator.CreateInstance(walletType, phrase, english, "", Enum.Parse(seedModeType, "Bip39"));
        var solAccount = walletType.GetProperty("Account").GetValue(solWallet);
        var solPublicKey = solAccount.GetType().GetProperty("PublicKey").GetValue(solAccount);
        var solTarget = (string)solPublicKey.GetType().GetProperty("Key").GetValue(solPublicKey);
        foreach (string chain in new[] { "ETH", "BSC", "SOL" })
        {
            string target = chain == "SOL" ? solTarget : evmAddress;
            requestType.GetProperty("Network").SetValue(request, chain);
            requestType.GetProperty("TargetAddress").SetValue(request, target);
            requestType.GetProperty("PhraseMask").SetValue(request, Enumerable.Repeat("abandon", 11).Concat(new[] { "?" }).ToArray());
            requestType.GetProperty("CandidateWords").SetValue(request, new[] { "about", "ability" });
            requestType.GetProperty("UseGpu").SetValue(request, true);
            requestType.GetProperty("Threads").SetValue(request, 2);
            requestType.GetMethod("Validate").Invoke(request, null);
            var recovered = search.Invoke(null, new object[] { request, null, CancellationToken.None, IntPtr.Zero, null });
            Check(((string)recovered.GetType().GetProperty("Engine").GetValue(recovered)).StartsWith("CPU") &&
                (string)recovered.GetType().GetProperty("Mnemonic").GetValue(recovered) == phrase &&
                string.Equals((string)recovered.GetType().GetProperty("Address").GetValue(recovered), target,
                    chain == "SOL" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase),
                "Missing OpenCL GPU recovers the known " + chain + " test wallet with CPU");
        }
        Check(((ListView)field("savedVanityList")).Items.Count == 3,
            "CPU-generated wallets appear in the saved wallets list");
    }

    private static void VerifyRecoveryPrivacy(Assembly assembly, Form form, TabPage recoveryTab, TabPage hitsTab)
    {
        const string address = "0x0000000000000000000000000000000000000001";
        const string fakeKey = "synthetic-private-key";
        const string fakeMnemonic = "synthetic mnemonic for privacy check";
        var result = assembly.CreateInstance("jmsTools.Manage.MnemonicRecoveryResult");
        var resultType = result.GetType();
        resultType.GetProperty("Address").SetValue(result, address);
        resultType.GetProperty("Network").SetValue(result, "ETH");
        resultType.GetProperty("Engine").SetValue(result, "Test");
        resultType.GetProperty("PrivateKey").SetValue(result, fakeKey);
        resultType.GetProperty("Mnemonic").SetValue(result, fakeMnemonic);

        var recovery = recoveryTab.Controls.OfType<Form>().Single();
        recovery.GetType().GetMethod("ShowFoundResult", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(recovery, new[] { result });
        var summary = recovery.Controls.Find("recoveryResult", true).Single().Text;
        Check(summary.Contains(address) && !summary.Contains(fakeKey) && !summary.Contains(fakeMnemonic),
            "Recovery summary displays the public address only");

        var hits = hitsTab.Controls.OfType<ListView>().Single();
        var item = hits.Items.Cast<ListViewItem>().Single();
        Check(item.SubItems.Count == 5 &&
            item.SubItems.Cast<ListViewItem.ListViewSubItem>().All(cell => !cell.Text.Contains(fakeKey) && !cell.Text.Contains(fakeMnemonic)),
            "Hit list cells contain no recovery secrets");
        var privateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        var row = (string)form.GetType().GetMethod("FormatFoundPublicRow", privateStatic).Invoke(null, new object[] { item });
        Check(row.Contains(address) && !row.Contains(fakeKey) && !row.Contains(fakeMnemonic),
            "Copy-public-row payload excludes secrets");
        using (var marker = new MemoryStream(new byte[] { 1 }))
        {
            var data = (DataObject)form.GetType().GetMethod("CreateWalletSecretClipboardData", privateStatic)
                .Invoke(null, new object[] { fakeKey, marker });
            Check(data.GetText(TextDataFormat.UnicodeText) == fakeKey &&
                data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing", false),
                "Explicit secret copy supplies the Windows history and sync exclusion marker");
        }
    }
}
