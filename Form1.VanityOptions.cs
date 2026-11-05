using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using jmsTools.Manage;
using Newtonsoft.Json;

namespace jmsTools
{
    public partial class Form1
    {
        private ComboBox vanityPatternMode, vanityTemplate, vanityTemplatePosition, vanityStopMode;
        private NumericUpDown vanityTargetCount;
        private Button vanityAdditionalButton;
        private string[] vanityAdditionalPatterns = new string[0];
        private Label vanityTemplateHelp, vanityFoundLabel, vanityPositionLabel;
        private long vanityMatchesFound;
        private readonly double[] vanityEstimateRates = new double[8];
        private bool layingOutVanity;
        private bool IsTemplateVanity => vanityPatternMode != null && vanityPatternMode.SelectedIndex == 1;

        private sealed class VanityOptions
        {
            public int Mode { get; set; }
            public string Template { get; set; } = "AAAA";
            public int Position { get; set; } = 1;
            public int StopMode { get; set; }
            public int Count { get; set; } = 1;
            public string[] AdditionalTemplates { get; set; }
        }

        private void InitializeVanityOptions()
        {
            vanityPatternMode = new ComboBox { Name = "vanityPatternMode", DropDownStyle = ComboBoxStyle.DropDownList };
            vanityPatternMode.Items.AddRange(new object[] { "固定前后缀", "模板模式" });
            vanityTemplatePosition = new ComboBox { Name = "vanityTemplatePosition", DropDownStyle = ComboBoxStyle.DropDownList };
            vanityTemplatePosition.Items.AddRange(new object[] { "头部", "尾部" });
            vanityTemplate = new ComboBox { Name = "vanityTemplate", DropDownStyle = ComboBoxStyle.DropDown,
                IntegralHeight = false, DropDownHeight = 340, DropDownWidth = 380 };
            for (int length = 3; length <= 12; length++)
                vanityTemplate.Items.Add(new string('A', length) + "（" + length + "位相同）");
            for (int length = 3; length <= 10; length++)
            {
                string sequence = "ABCDEFGHIJ".Substring(0, length);
                vanityTemplate.Items.Add(sequence + "（" + length + "位递增）");
                vanityTemplate.Items.Add(new string(sequence.Reverse().ToArray()) + "（" + length + "位递减）");
            }
            vanityTemplate.Items.AddRange(new object[] {
                "ABAB（交替重复）", "ABABAB（3组交替）", "ABABABAB（4组交替）", "ABABABABAB（5组交替）", "ABABABABABAB（6组交替）",
                "AABB（成对重复）", "AABBCC（3对重复）", "AABBCCDD（4对重复）", "AABBCCDDEE（5对重复）", "AABBCCDDEEFF（6对重复）",
                "AAABBB（两组三连）", "AAAABBBB（两组四连）", "AAABBBCCC（3组三连）", "AAAABBBBCCCC（3组四连）",
                "ABBA（4位对称）", "ABCBA（5位对称）", "ABCCBA（6位对称）", "ABCDDCBA（8位对称）", "ABCDEEDCBA（10位对称）", "ABCDEFFEDCBA（12位对称）",
                "ABCABC（3位循环）", "ABCABCABC（3组循环）", "ABCABCABCABC（4组循环）", "ABCDABCD（4位循环）", "ABCDABCDABCD（3组循环）",
                "ABCA（首尾相同）", "AABAA（中心对称）", "AABAAB（重复组合）"
            });
            vanityStopMode = new ComboBox { Name = "vanityStopMode", DropDownStyle = ComboBoxStyle.DropDownList };
            vanityStopMode.Items.AddRange(new object[] { "找到 1 个停止", "找到指定数量停止", "持续生成（手动停止）" });
            vanityTargetCount = new NumericUpDown { Name = "vanityTargetCount", Minimum = 1, Maximum = 10000, Value = 1 };
            vanityTemplateHelp = new Label { Name = "vanityTemplateHelp", AutoEllipsis = true, ForeColor = Color.DimGray };
            vanityFoundLabel = new Label { Name = "vanityFoundLabel", AutoSize = true, Text = "本轮已保存：0 个" };
            vanityAdditionalButton = new Button { Name = "vanityAdditionalButton" };
            vanityAdditionalButton.Click += (sender, args) => EditAdditionalVanityTemplates();
            vanityPositionLabel = new Label { AutoSize = true, Text = "位置：" };
            生成配置.Controls.AddRange(new Control[] {
                new Label { Text = "生成方式：", Location = new Point(23, 59), AutoSize = true },
                vanityPatternMode, vanityTemplatePosition, vanityPositionLabel, vanityTemplate,
                new Label { Text = "停止条件：", Location = new Point(23, 215), AutoSize = true },
                vanityStopMode, vanityTargetCount,
                new Label { Text = "个", Location = new Point(389, 215), AutoSize = true },
                vanityTemplateHelp, vanityFoundLabel, vanityAdditionalButton
            });
            var options = new VanityOptions();
            try { options = JsonConvert.DeserializeObject<VanityOptions>(File.ReadAllText(VanityOptionsPath)) ?? options; } catch { }
            vanityPatternMode.SelectedIndex = options.Mode == 1 ? 1 : 0;
            vanityTemplatePosition.SelectedIndex = options.Position == 0 ? 0 : 1;
            vanityTemplate.Text = options.Template ?? "AAAA";
            vanityStopMode.SelectedIndex = options.StopMode >= 0 && options.StopMode <= 2 ? options.StopMode : 0;
            vanityTargetCount.Value = Math.Max(1, Math.Min(10000, options.Count));
            vanityAdditionalPatterns = (options.AdditionalTemplates ?? new string[0]).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Take(5).ToArray();
            vanityPatternMode.SelectedIndexChanged += (sender, args) => RefreshVanityOptions();
            vanityTemplatePosition.SelectedIndexChanged += (sender, args) => RefreshVanityPattern();
            vanityTemplate.TextChanged += (sender, args) => RefreshVanityPattern();
            vanityStopMode.SelectedIndexChanged += (sender, args) => RefreshVanityOptions();
            vanityTargetCount.ValueChanged += (sender, args) => RefreshVanityPattern();
            vanityNetwork.SelectedIndexChanged += (sender, args) => RefreshVanityOptions();
            saveVanityBatch.Click += (sender, args) => SaveVanityOptions();
            FormClosing += (sender, args) => SaveVanityOptions();
            var estimateTip = new ToolTip(components);
            estimateTip.SetToolTip(ygtime, "首中时间是约 50% 命中所需的搜索时间；多个结果显示剩余数量的平均搜索时间。估算不含初始化和保存，也不是倒计时。\nSOL 按 Base58 字符分布粗估，多模板部分重叠时可能偏乐观；EVM 多模板按轮流搜索估算，不含切换开销。");
            ygtime.AutoSize = false;
            ygtime.AutoEllipsis = true;
            labbel10.Text = "50%首中：";
            tabPage3.AutoScroll = true;
            生成配置.Anchor = groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            生成配置.Text = "搜索条件";
            groupBox1.Text = "搜索进度与结果";
            labelResults.ReadOnly = true;
            labelResults.BackColor = SystemColors.Window;
            labelResults.ScrollBars = ScrollBars.Vertical;
            labelResults.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            generate_btn.FlatStyle = FlatStyle.Flat;
            generate_btn.FlatAppearance.BorderSize = 0;
            generate_btn.UseVisualStyleBackColor = false;
            generate_btn.EnabledChanged += (sender, args) => StyleVanityStart();
            StyleVanityStart();
            tabPage3.Layout += (sender, args) => LayoutVanityOptions();
            LayoutVanityOptions();
            RefreshVanityOptions();
        }

        private void StyleVanityStart()
        {
            generate_btn.BackColor = generate_btn.Enabled ? Color.FromArgb(29, 85, 171) : Color.FromArgb(213, 221, 231);
            generate_btn.ForeColor = generate_btn.Enabled ? Color.White : Color.FromArgb(54, 69, 91);
        }

        private static string VanityOptionsPath => Path.Combine(AppPaths.DataDirectory, "VanityOptions.json");

        private void SaveVanityOptions()
        {
            if (vanityPatternMode == null) return;
            var options = new VanityOptions {
                Mode = vanityPatternMode.SelectedIndex, Template = vanityTemplate.Text,
                Position = vanityTemplatePosition.SelectedIndex, StopMode = vanityStopMode.SelectedIndex,
                Count = (int)vanityTargetCount.Value, AdditionalTemplates = vanityAdditionalPatterns
            };
            var temporary = VanityOptionsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(options, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(VanityOptionsPath)) File.Replace(temporary, VanityOptionsPath, null);
                else File.Move(temporary, VanityOptionsPath);
            }
            catch { if (!Disposing && !IsDisposed) vanityGpuStatus.Text = "生成偏好无法保存，本次选择仍可使用。"; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }

        private VanityTemplate RequestedVanityTemplate() => IsTemplateVanity
            ? VanityTemplate.Parse(vanityTemplate.Text, vanityTemplatePosition.SelectedIndex == 1, IsSolVanity) : null;

        private VanityTemplate[] RequestedVanityTemplates(VanityTemplate primary)
        {
            if (primary == null) return new VanityTemplate[0];
            return new[] { primary }.Concat(vanityAdditionalPatterns.Select(value => VanityTemplate.Parse(value, primary.AtEnd, IsSolVanity)))
                .GroupBy(value => value.Pattern).Select(group => group.First()).ToArray();
        }

        private void EditAdditionalVanityTemplates()
        {
            using (var dialog = new Form { Text = "附加靓号模板", ClientSize = new Size(455, 440), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                var hint = new Label { Location = new Point(12, 10), Size = new Size(431, 44), Text =
                    "最多 5 个，与主模板使用相同的头尾位置及大小写设置。\nEVM GPU 按模板轮流搜索，其他引擎同时匹配。" };
                var list = new CheckedListBox { Location = new Point(12, 55), Size = new Size(431, 226), CheckOnClick = true };
                var presetNames = new System.Collections.Generic.HashSet<string>();
                foreach (string item in vanityTemplate.Items.Cast<string>().Concat(new[] { "AABBAABBAABB（成对循环）" }))
                {
                    string pattern = item.Split('（')[0];
                    presetNames.Add(pattern);
                    list.Items.Add(item, vanityAdditionalPatterns.Contains(pattern));
                }
                var inputHint = new Label { Location = new Point(12, 288), Size = new Size(431, 21), Text = "自定义模板（用空格、逗号或换行分隔）：" };
                var input = new TextBox { Location = new Point(12, 309), Size = new Size(431, 63), Multiline = true,
                    Text = string.Join(Environment.NewLine, vanityAdditionalPatterns.Where(value => !presetNames.Contains(value))), MaxLength = 256 };
                var error = new Label { Location = new Point(12, 375), Size = new Size(431, 23), ForeColor = Color.Firebrick };
                var save = new Button { Text = "保存", Location = new Point(278, 403), Size = new Size(78, 26) };
                var cancel = new Button { Text = "取消", Location = new Point(365, 403), Size = new Size(78, 26), DialogResult = DialogResult.Cancel };
                save.Click += (sender, args) => {
                    try
                    {
                        var selected = list.CheckedItems.Cast<string>().Concat(input.Text.Split(new[] { ' ', '\t', '\r', '\n', ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
                            .Select(value => VanityTemplate.Parse(value, vanityTemplatePosition.SelectedIndex == 1, IsSolVanity).Pattern).Distinct().ToArray();
                        if (selected.Length > 5) throw new ArgumentException("最多可选择 5 个附加模板。");
                        vanityAdditionalPatterns = selected;
                        dialog.DialogResult = DialogResult.OK;
                    }
                    catch (ArgumentException ex) { error.Text = ex.Message; }
                };
                dialog.Controls.AddRange(new Control[] { hint, list, inputHint, input, error, save, cancel });
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK) { RefreshVanityOptions(); SaveVanityOptions(); }
            }
        }

        private void RefreshVanityOptions()
        {
            if (vanityPatternMode == null) return;
            bool template = IsTemplateVanity;
            textBoxPrefix.Visible = textBoxSuffix.Visible = label6.Visible = !template;
            label5.Text = template ? "模板：" : "前缀：";
            vanityTemplate.Visible = vanityTemplatePosition.Visible = vanityPositionLabel.Visible = template;
            vanityAdditionalButton.Visible = template;
            vanityAdditionalButton.Enabled = !vanityRunning;
            vanityAdditionalButton.Text = "附加模板 (" + vanityAdditionalPatterns.Length + ")";
            vanityPatternMode.Enabled = vanityTemplate.Enabled = vanityTemplatePosition.Enabled = vanityStopMode.Enabled = !vanityRunning;
            vanityTargetCount.Enabled = !vanityRunning && vanityStopMode.SelectedIndex == 1;
            vanityTemplateHelp.Text = template
                ? vanityAdditionalPatterns.Length == 0 ? "可输入3–12位；同字母同字符，异字母不同。" :
                    (IsSolVanity ? "同时匹配：" : "轮流搜索：") + string.Join("、", vanityAdditionalPatterns)
                : "前后缀填写需要的实际字符；可设置找到几个后停止。";
            RefreshVanityPattern();
        }

        private bool RefreshVanityTemplatePreview()
        {
            if (!IsTemplateVanity) return false;
            try
            {
                var template = RequestedVanityTemplate();
                string sample = IsSolVanity ? "7YttLkHDoNj9wyDur5iKf9TSPfNE7pExvKjV9EY4Q5qi" : originalEthAddress.Substring(2);
                string example = template.Example(IsSolVanity);
                xample_str.Text = (IsSolVanity ? "" : "0x") + (template.AtEnd ? sample.Substring(0, sample.Length - example.Length) + example : example + sample.Substring(example.Length));
                RefreshVanityEstimate(VanityEstimate.ExpectedTemplateSet(IsSolVanity, checkBox1.Checked, RequestedVanityTemplates(template)));
            }
            catch (ArgumentException error)
            {
                xample_str.Text = error.Message;
                complexity_lab.Text = probability_lab.Text = "—"; ygtime.Text = "请检查模板";
            }
            return true;
        }

        private static int VanityRateIndex(bool sol, bool template, bool gpu) => (gpu ? 4 : 0) + (sol ? 2 : 0) + (template ? 1 : 0);

        private void RefreshVanityEstimate(double expected)
        {
            complexity_lab.Text = "约 " + FormatVanityNumber(expected) + " 次/个";
            probability_lab.Text = "约 " + FormatVanityNumber(VanityEstimate.MedianAttempts(expected)) + " 次";
            int mode = vanityStopMode?.SelectedIndex ?? 0;
            double rate = vanityRunning ? 0 : vanityEstimateRates[VanityRateIndex(IsSolVanity, IsTemplateVanity, PreferVanityGpu)];
            ShowVanityEstimateTime(expected, mode, mode == 1 ? (long)vanityTargetCount.Value : 1, rate, rate > 0);
        }

        private void ShowVanityEstimateTime(double expected, int mode, long remaining, double rate, bool cached = false)
        {
            if (rate <= 0) { ygtime.Text = "等待实测速率"; return; }
            string source = cached ? "上次速率 · " : "";
            double attempts = mode == 1 ? expected * remaining : VanityEstimate.MedianAttempts(expected);
            string time = FormatVanityTime(attempts / rate);
            ygtime.Text = source + (mode == 1 ? "剩 " + remaining + " 个，平均约 " : mode == 2 ? "下个 50% 约 " : "50% 命中约 ") + time;
        }

        private void UpdateVanityFoundCount()
        {
            string target = vanityStopMode.SelectedIndex == 1 ? " / " + vanityTargetCount.Value.ToString("0") : "";
            vanityFoundLabel.Text = "本轮已保存：" + vanityMatchesFound + target + " 个";
        }

        private void LayoutVanityOptions()
        {
            if (layingOutVanity || vanityPatternMode == null || IsDisposed || Disposing || tabPage3.IsDisposed || tabPage3.Disposing) return;
            layingOutVanity = true;
            tabPage3.SuspendLayout();
            try
            {
                bool narrow = tabPage3.Width < 850;
                int width = narrow ? Math.Max(455, tabPage3.Width - SystemInformation.VerticalScrollBarWidth - 24) : 469;
                int detailHeight = narrow ? 270 : Math.Max(374, tabPage3.ClientSize.Height - 24);
                Point scroll = tabPage3.AutoScrollPosition;
                生成配置.SetBounds(12 + scroll.X, 12 + scroll.Y, width, 374);
                groupBox1.SetBounds((narrow ? 12 : 493) + scroll.X, (narrow ? 398 : 12) + scroll.Y,
                    narrow ? width : Math.Max(300, tabPage3.Width - (tabPage3.ClientSize.Height < 400 ? SystemInformation.VerticalScrollBarWidth : 0) - 505), detailHeight);
                ygtime.SetBounds(94, 108, Math.Max(100, groupBox1.ClientSize.Width - 106), 30);
                label11.Location = new Point(24, 146);
                labelResults.SetBounds(22, 174, groupBox1.ClientSize.Width - 44, detailHeight - 194);
                probability_lab.Left = 94;
                tabPage3.AutoScrollMinSize = new Size(0, narrow ? 680 : 398);
                vanityNetwork.SetBounds(88, 25, 335, 21);
                vanityPatternMode.SetBounds(88, 55, 163, 21);
                vanityPositionLabel.Location = new Point(270, 59);
                vanityTemplatePosition.SetBounds(315, 55, 108, 21);
                label5.Location = new Point(41, 89); label6.Location = new Point(222, 89);
                textBoxPrefix.SetBounds(88, 85, 100, 21); textBoxSuffix.SetBounds(269, 85, 154, 21);
                vanityTemplate.SetBounds(88, 85, 211, 21);
                vanityAdditionalButton.SetBounds(309, 84, 114, 24);
                vanityTemplateHelp.SetBounds(41, 114, 389, 29);
                checkBox1.Location = new Point(43, 151);
                var engineLabel = 生成配置.Controls.OfType<Label>().First(control => control.Text == "计算引擎：");
                engineLabel.Location = new Point(199, 155); vanityEngine.SetBounds(269, 151, 154, 21);
                vanityBatchLabel.Location = new Point(41, 185);
                vanityBatch.SetBounds(155, 181, 195, 21); saveVanityBatch.SetBounds(363, 180, 60, 23);
                vanityStopMode.SetBounds(88, 211, 210, 21); vanityTargetCount.SetBounds(309, 211, 73, 21);
                vanityFoundLabel.Location = new Point(41, 243);
                label4.Location = new Point(41, 269); xample_str.SetBounds(88, 265, 335, 20);
                generate_btn.SetBounds(41, 295, 114, 30); stop_btn.SetBounds(166, 295, 88, 30);
                generate_btn.Text = "开始生成";
                viewSavedVanities.SetBounds(266, 295, 171, 30); viewSavedVanities.AutoEllipsis = true;
                vanityGpuStatus.SetBounds(43, 335, width - 64, 29);
            }
            finally { tabPage3.ResumeLayout(true); layingOutVanity = false; }
        }
    }
}
