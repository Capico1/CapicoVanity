using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using jmsTools.Manage;

namespace jmsTools
{
    public sealed class MnemonicRecoveryForm : Form
    {
        private readonly Action<MnemonicRecoveryResult> onFound;
        private readonly System.Windows.Forms.ComboBox network;
        private readonly System.Windows.Forms.TextBox address;
        private readonly System.Windows.Forms.TextBox wordlistPath;
        private readonly System.Windows.Forms.Button chooseWordlist;
        private readonly System.Windows.Forms.TextBox phraseMask;
        private readonly CheckBox showPhrase;
        private readonly System.Windows.Forms.TextBox passphrase;
        private readonly NumericUpDown addressIndex;
        private readonly NumericUpDown batchSize;
        private readonly CheckBox autoBatch;
        private readonly NumericUpDown threads;
        private readonly System.Windows.Forms.ComboBox engineChoice;
        private readonly System.Windows.Forms.Button start;
        private readonly System.Windows.Forms.Button stop;
        private readonly Label estimate;
        private readonly Label progress;
        private readonly System.Windows.Forms.TextBox resultText;
        private CancellationTokenSource cancellation;
        private bool running;

        public MnemonicRecoveryForm(Action<MnemonicRecoveryResult> onFound = null, string defaultWordlist = null, Action viewResults = null)
        {
            this.onFound = onFound;
            Text = "目标地址助记词找回";
            Name = "mnemonicRecoveryForm";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 570);
            MinimumSize = new Size(720, 520);
            Padding = new Padding(12);
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            Controls.Add(root);

            var main = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8,
                AutoScroll = true, Padding = new Padding(0)
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.Controls.Add(main, 0, 0);

            network = new System.Windows.Forms.ComboBox {
                Name = "recoveryNetwork", DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 86, Margin = new Padding(5, 3, 12, 3)
            };
            network.Items.AddRange(new object[] { "ETH", "BSC", "SOL" });
            network.SelectedIndex = 0;
            address = new System.Windows.Forms.TextBox {
                Name = "recoveryAddress", Width = 378, Margin = new Padding(5, 3, 3, 3)
            };
            main.Controls.Add(Flow(
                Label("恢复网络："), network, Label("目标公开地址："), address), 0, 0);

            engineChoice = new System.Windows.Forms.ComboBox {
                Name = "recoveryEngine", DropDownStyle = ComboBoxStyle.DropDownList, Width = 136,
                Margin = new Padding(5, 3, 12, 3)
            };
            engineChoice.Items.AddRange(new object[] { "自动（GPU 优先）", "仅 CPU" });
            engineChoice.SelectedIndex = 0;
            batchSize = new NumericUpDown {
                Name = "recoveryGpuBatch", Width = 105, Minimum = 1, Maximum = 1048576,
                Increment = 8192, Value = 32768, ThousandsSeparator = true,
                Margin = new Padding(5, 3, 18, 3)
            };
            threads = new NumericUpDown {
                Name = "recoveryThreads", Width = 64, Minimum = 1, Maximum = 128,
                Value = Math.Max(1, Math.Min(128, Environment.ProcessorCount)),
                Margin = new Padding(5, 3, 3, 3)
            };
            autoBatch = new CheckBox {
                Name = "recoveryAutoBatch", Text = "自动优化（推荐）", AutoSize = true, Checked = true,
                Margin = new Padding(5, 7, 18, 3)
            };
            batchSize.Enabled = false;
            autoBatch.CheckedChanged += (sender, args) => RefreshEngineControls();
            engineChoice.SelectedIndexChanged += (sender, args) => RefreshEngineControls();
            var batchPanel = Flow(autoBatch, Label("手动每批："), batchSize);
            batchPanel.AutoSize = true;
            batchPanel.Dock = DockStyle.None;
            var cpuPanel = Flow(Label("CPU 线程："), threads);
            cpuPanel.AutoSize = true;
            cpuPanel.Dock = DockStyle.None;
            engineChoice.SelectedIndexChanged += (sender, args) => {
                batchPanel.Visible = engineChoice.SelectedIndex == 0;
                cpuPanel.Visible = engineChoice.SelectedIndex == 1;
            };
            main.Controls.Add(Flow(engineChoice, batchPanel, cpuPanel), 0, 1);
            cpuPanel.Visible = false;
            RefreshEngineControls();

            wordlistPath = new System.Windows.Forms.TextBox {
                Name = "recoveryWordlistPath", Width = 390, Margin = new Padding(5, 3, 6, 3),
                Text = defaultWordlist ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wordlist.txt")
            };
            chooseWordlist = new System.Windows.Forms.Button {
                Name = "recoveryChooseWordlist", Text = "选择词表…", AutoSize = true,
                Margin = new Padding(2, 1, 3, 3)
            };
            chooseWordlist.Click += (sender, args) => ChooseWordlist();
            main.Controls.Add(Flow(Label("候选词表："), wordlistPath, chooseWordlist), 0, 2);

            phraseMask = new System.Windows.Forms.TextBox {
                Name = "recoveryPhraseMask", Width = 460, UseSystemPasswordChar = true, Margin = new Padding(5, 3, 3, 3),
                Text = "? ? ? ? ? ? ? ? ? ? ? ?"
            };
            phraseMask.TextChanged += (sender, args) => UpdateEstimate();
            showPhrase = new CheckBox { Name = "recoveryShowPhrase", Text = "显示", AutoSize = true, Margin = new Padding(5, 7, 3, 3) };
            showPhrase.CheckedChanged += (sender, args) => phraseMask.UseSystemPasswordChar = !showPhrase.Checked;
            main.Controls.Add(Flow(Label("12 词模板："), phraseMask, showPhrase), 0, 3);

            passphrase = new System.Windows.Forms.TextBox {
                Name = "recoveryPassphrase", Width = 280, UseSystemPasswordChar = true,
                Margin = new Padding(5, 3, 22, 3)
            };
            addressIndex = new NumericUpDown {
                Name = "recoveryAddressIndex", Width = 100, Minimum = 0,
                Maximum = 999999, Value = 0, ThousandsSeparator = true,
                Margin = new Padding(5, 3, 3, 3)
            };
            main.Controls.Add(Flow(Label("BIP39 密码："), passphrase,
                Label("ETH/BSC 地址索引："), addressIndex), 0, 4);

            estimate = new Label {
                Name = "recoveryEstimate", Dock = DockStyle.Fill, AutoSize = false,
                ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 5, 0)
            };
            main.Controls.Add(estimate, 0, 5);

            progress = new Label {
                Name = "recoveryProgress", Dock = DockStyle.Fill, AutoSize = false,
                Text = "尚未开始。空位按词表顺序组合；未知词用 ? 或 _ 表示。",
                TextAlign = ContentAlignment.TopLeft, Padding = new Padding(5, 5, 5, 5)
            };
            main.Controls.Add(progress, 0, 6);

            start = new System.Windows.Forms.Button {
                Name = "recoveryStart", Text = "开始找回", AutoSize = true,
                Margin = new Padding(5, 4, 8, 3)
            };
            stop = new System.Windows.Forms.Button {
                Name = "recoveryStop", Text = "停止搜索", AutoSize = true,
                Enabled = false, Margin = new Padding(3, 4, 8, 3)
            };
            start.Click += async (sender, args) => await StartSearchAsync();
            stop.Click += (sender, args) => {
                stop.Enabled = false;
                if (cancellation != null) cancellation.Cancel();
                progress.Text = "正在停止；当前批次结束后退出。";
            };
            var actions = Flow(start, stop);
            if (viewResults != null)
            {
                var results = new System.Windows.Forms.Button { Name = "recoveryViewResults", Text = "查看命中列表", AutoSize = true, Margin = new Padding(5, 4, 8, 3) };
                results.Click += (sender, args) => viewResults();
                actions.Controls.Add(results);
            }
            main.Controls.Add(actions, 0, 7);

            resultText = new System.Windows.Forms.TextBox {
                Name = "recoveryResult", Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill
            };
            root.Controls.Add(resultText, 0, 1);

            network.SelectedIndexChanged += (sender, args) => {
                bool sol = SelectedNetwork == "SOL";
                addressIndex.Enabled = !running && !sol;
                UpdateEstimate();
            };
            wordlistPath.TextChanged += (sender, args) => UpdateEstimate();
            FormClosing += (sender, args) => { if (StopBeforeClosing()) args.Cancel = true; };
            UpdateEstimate();
        }

        internal bool StopBeforeClosing()
        {
            if (!running || cancellation == null) return false;
            cancellation.Cancel();
            stop.Enabled = false;
            progress.Text = "正在停止；当前批次结束后即可关闭。";
            return true;
        }

        private string SelectedNetwork => network.SelectedItem == null ? "ETH" : network.SelectedItem.ToString();

        private static FlowLayoutPanel Flow(params Control[] controls)
        {
            var panel = new FlowLayoutPanel {
                Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false,
                Padding = new Padding(0), Margin = new Padding(0)
            };
            panel.Controls.AddRange(controls);
            return panel;
        }

        private static Label Label(string text, Color? color = null)
        {
            return new Label {
                Text = text, AutoSize = true, Margin = new Padding(5, 7, 2, 3),
                ForeColor = color ?? SystemColors.ControlText
            };
        }

        private void ChooseWordlist()
        {
            using (var dialog = new OpenFileDialog {
                Filter = "文本词表 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = wordlistPath.Text, CheckFileExists = true
            })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    wordlistPath.Text = dialog.FileName;
                    UpdateEstimate();
                }
        }

        private string[] ReadCandidateWords()
        {
            if (!File.Exists(wordlistPath.Text))
                throw new FileNotFoundException("找不到候选词表。", wordlistPath.Text);
            return File.ReadAllLines(wordlistPath.Text)
                .SelectMany(line => line.Split(new[] { ' ', '\t', ',', ';' },
                    StringSplitOptions.RemoveEmptyEntries))
                .Select(word => word.Trim().ToLowerInvariant())
                .Where(word => word.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private string[] ReadPhraseMask()
        {
            var parts = (phraseMask.Text ?? "").Split(new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 12) throw new ArgumentException("12 词模板必须正好包含 12 个位置。");
            return parts;
        }

        private void UpdateEstimate()
        {
            if (estimate == null) return;
            try
            {
                var mask = ReadPhraseMask();
                var words = ReadCandidateWords();
                string count = MnemonicRecoveryService.EstimateSearchSpace(
                    mask, words, false);
                int unknown = mask.Count(value => value == "?" || value == "_");
                estimate.Text = "候选词 " + words.Length.ToCultureString() +
                    " 个；空位 " + unknown + " 个；不重复组合上限 " + count +
                    "。完整 BIP39 词表直接穷举 12 个空位不可行，建议填写已记住的词位置。";
            }
            catch (Exception ex)
            {
                estimate.Text = ex.Message;
            }
        }

        private async Task StartSearchAsync()
        {
            if (running) return;
            MnemonicRecoveryRequest request;
            try
            {
                request = new MnemonicRecoveryRequest {
                    Network = SelectedNetwork,
                    TargetAddress = address.Text.Trim(),
                    PhraseMask = ReadPhraseMask(),
                    CandidateWords = ReadCandidateWords(),
                    Passphrase = passphrase.Text,
                    AddressIndex = (int)addressIndex.Value,
                    AllowRepeatedWords = false,
                    UseGpu = engineChoice.SelectedIndex == 0,
                    GpuBatchSize = autoBatch.Checked ? 0 : (int)batchSize.Value,
                    Threads = (int)threads.Value
                };
                request.Validate();
            }
            catch (Exception ex)
            {
                progress.Text = ex.Message;
                return;
            }

            running = true;
            SetRunningState(true);
            cancellation = new CancellationTokenSource();
            resultText.Clear();
            progress.Text = "正在准备 " + request.Network + " 恢复任务…";
            try
            {
                var reporter = new Progress<MnemonicRecoveryProgress>(value => {
                    if (IsDisposed || !running) return;
                    bool gpu = value.Engine.StartsWith("GPU", StringComparison.Ordinal);
                    progress.Text = "引擎：" + request.Network + " / " + value.Engine +
                        (gpu ? " · " + (request.GpuBatchSize == 0 ? "自动优化" : "手动批次") : "") +
                        (value.GpuBatchSize > 0 ? " · 上批 " + value.GpuBatchSize.ToCultureString() + " 个" : "") +
                        "\r\n已组合：" + value.CombinationsChecked.ToScientificString() +
                        "；BIP39 校验通过：" + value.ChecksumValid.ToScientificString() +
                        "；速度：" + value.CombinationsPerSecond.ToScientificString() + " 组合/秒";
                });
                var found = await MnemonicRecoveryService.SearchAsync(
                    request, reporter, cancellation.Token);
                ShowFoundResult(found);
            }
            catch (OperationCanceledException)
            {
                progress.Text = "搜索已停止。";
            }
            catch (Exception ex)
            {
                progress.Text = ex.Message;
            }
            finally
            {
                cancellation.Dispose();
                cancellation = null;
                running = false;
                SetRunningState(false);
            }
        }

        private void ShowFoundResult(MnemonicRecoveryResult found)
        {
            resultText.Text = "已找到匹配钱包\r\n地址：" + found.Address +
                "\r\n助记词和私钥已隐藏。需要时，请在命中列表中单独复制。" +
                "\r\n引擎：" + found.Engine +
                (found.Network == "SOL" ? "" : "；地址索引：" + found.AddressIndex);
            if (onFound != null) onFound(found);
            progress.Text = "已找到并通过地址复核，结果已加入本次命中列表。";
        }

        private void SetRunningState(bool value)
        {
            if (value) showPhrase.Checked = false;
            showPhrase.Enabled = !value;
            start.Enabled = stop.Enabled = !value;
            start.Enabled = !value;
            stop.Enabled = value;
            network.Enabled = address.Enabled = wordlistPath.Enabled = chooseWordlist.Enabled =
                phraseMask.Enabled = passphrase.Enabled = !value;
            addressIndex.Enabled = !value && SelectedNetwork != "SOL";
            RefreshEngineControls();
        }

        private void RefreshEngineControls()
        {
            bool automatic = engineChoice.SelectedIndex == 0;
            engineChoice.Enabled = !running;
            autoBatch.Enabled = !running && automatic;
            batchSize.Enabled = !running && automatic && !autoBatch.Checked;
            threads.Enabled = !running && !automatic;
        }
    }

    internal static class RecoveryNumberFormatting
    {
        public static string ToCultureString(this int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string ToScientificString(this long value)
        {
            if (value < 1000000) return value.ToString("N0", CultureInfo.InvariantCulture);
            return value.ToString("0.00E+0", CultureInfo.InvariantCulture);
        }

        public static string ToScientificString(this double value)
        {
            if (value < 1000000) return value.ToString("N0", CultureInfo.InvariantCulture);
            return value.ToString("0.00E+0", CultureInfo.InvariantCulture);
        }
    }
}
