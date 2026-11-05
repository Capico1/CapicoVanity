using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using jmsTools.Manage;

namespace jmsTools
{
    public partial class Form1
    {
        private VanityGpuSettings vanityBatchSettings;
        private System.Windows.Forms.ComboBox vanityBatch;
        private System.Windows.Forms.Button saveVanityBatch;
        private Label vanityBatchLabel;
        private bool changingVanityBatch;

        private void InitializeVanityBatchControls()
        {
            bool loadFailed = false;
            try { vanityBatchSettings = VanityGpuSettings.Load(); }
            catch (Exception) { vanityBatchSettings = new VanityGpuSettings(); loadFailed = true; }
            foreach (Control control in 生成配置.Controls)
                if (control.Top >= 160) control.Top += 34;
            生成配置.Height = 350;
            vanityBatchLabel = new Label { AutoSize = true, Location = new Point(41, 166) };
            vanityBatch = new System.Windows.Forms.ComboBox {
                Name = "vanityBatch", DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(155, 161), Size = new Size(195, 21)
            };
            saveVanityBatch = new System.Windows.Forms.Button {
                Name = "saveVanityBatch", Text = "保存", Location = new Point(363, 160), Size = new Size(60, 24)
            };
            生成配置.Controls.AddRange(new Control[] { vanityBatchLabel, vanityBatch, saveVanityBatch });
            vanityBatch.SelectedIndexChanged += (sender, args) => {
                if (changingVanityBatch || vanityBatch.SelectedIndex < 0) return;
                if (IsSolVanity) vanityBatchSettings.SolBatchSize = VanityGpuSettings.SolChoices[vanityBatch.SelectedIndex];
                else vanityBatchSettings.EvmBatchMultiple = VanityGpuSettings.EvmChoices[vanityBatch.SelectedIndex];
                vanityGpuStatus.Text = "批次已选择，下次生成生效；点“保存”可在重开后保留。";
            };
            saveVanityBatch.Click += (sender, args) => {
                try { vanityBatchSettings.Save(); vanityGpuStatus.Text = "EVM / SOL 靓号批次已分别保存，下次生成生效。"; }
                catch (Exception) { vanityGpuStatus.Text = "批次保存失败，请检查程序目录权限；本次选择仍可用于生成。"; }
            };
            RefreshVanityBatchControls();
            if (loadFailed) vanityGpuStatus.Text = "批次配置无法读取，已恢复原默认值；重新选择并保存即可。";
        }

        private void RefreshVanityBatchControls()
        {
            if (vanityBatch == null) return;
            changingVanityBatch = true;
            try
            {
                vanityBatch.Items.Clear();
                vanityBatchLabel.Text = IsSolVanity ? "GPU 每批地址数：" : "GPU 批次倍率：";
                if (IsSolVanity)
                {
                    foreach (var value in VanityGpuSettings.SolChoices)
                        vanityBatch.Items.Add(value == 0 ? "自动优化（推荐）" : value.ToString("N0", CultureInfo.InvariantCulture) + (value == 65536 ? "（原默认）" : ""));
                    vanityBatch.SelectedIndex = Array.IndexOf(VanityGpuSettings.SolChoices, vanityBatchSettings.SolBatchSize);
                }
                else
                {
                    foreach (var value in VanityGpuSettings.EvmChoices)
                        vanityBatch.Items.Add(value == 0 ? "自动优化（推荐）" : value.ToString("N0", CultureInfo.InvariantCulture) + " 倍" +
                            (value == 16384 ? "（更耗显存）" : ""));
                    vanityBatch.SelectedIndex = Array.IndexOf(VanityGpuSettings.EvmChoices, vanityBatchSettings.EvmBatchMultiple);
                }
                vanityBatch.Enabled = saveVanityBatch.Enabled = !vanityRunning && PreferVanityGpu;
            }
            finally { changingVanityBatch = false; }
        }
    }
}
