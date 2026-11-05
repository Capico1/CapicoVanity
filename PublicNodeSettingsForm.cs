using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using jmsTools.Manage;

namespace jmsTools
{
    internal sealed class PublicNodeSettingsForm : Form
    {
        private readonly TabControl networks = new TabControl();
        private readonly Dictionary<string, TextBox[]> inputs = new Dictionary<string, TextBox[]>();
        private readonly Dictionary<string, string[]> saved = new Dictionary<string, string[]>();
        private readonly Label status = new Label();

        public PublicNodeSettingsForm(string selectedNetwork)
        {
            Text = "手动地址查询 · 节点设置";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(740, 350);
            MinimumSize = new Size(600, 389);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            networks.Name = "nodeNetworks";
            networks.Size = new Size(740, 280);
            networks.Dock = DockStyle.Fill;
            foreach (var chain in new[] { "ETH", "BSC", "SOL" })
            {
                var page = new TabPage(chain) { Name = chain, Padding = new Padding(12), Size = new Size(732, 254) };
                var urls = BlockManage.ReadPublicNodeUrls(chain);
                var fields = new TextBox[3];
                for (int i = 0; i < fields.Length; i++)
                {
                    int row = i;
                    page.Controls.Add(new Label { Text = i == 0 ? "首选节点" : "备用节点 " + i,
                        AutoSize = true, Location = new Point(12, 24 + i * 53) });
                    fields[i] = new TextBox { Name = "nodeUrl" + i, Text = i < urls.Length ? urls[i] : "",
                        Location = new Point(100, 20 + i * 53), Size = new Size(534, 25),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    page.Controls.Add(fields[i]);
                    var clear = new Button { Name = "clearNode" + i, Text = "清空",
                        Location = new Point(644, 18 + i * 53), Size = new Size(52, 27),
                        Anchor = AnchorStyles.Top | AnchorStyles.Right };
                    clear.Click += (sender, args) => fields[row].Clear();
                    page.Controls.Add(clear);
                }
                page.Controls.Add(new Label { Text = "直接粘贴完整 HTTPS RPC 地址；需要 API Key 时，粘贴服务商提供的完整地址。\n空白项不保存；每个网络分别保存。保存后下一次手动查询生效。",
                    Location = new Point(12, 185), Size = new Size(680, 55),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
                inputs.Add(chain, fields);
                saved.Add(chain, fields.Select(field => field.Text).ToArray());
                networks.TabPages.Add(page);
                if (chain == selectedNetwork) networks.SelectedTab = page;
            }
            var footer = new Panel { Dock = DockStyle.Bottom, Size = new Size(740, 70) };
            status.Name = "nodeSaveStatus";
            status.Location = new Point(12, 8);
            status.Size = new Size(710, 22);
            status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var save = new Button { Name = "saveNodeSettings", Text = "保存当前网络",
                Location = new Point(482, 35), Size = new Size(130, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            save.Click += (sender, args) => SaveCurrentNetwork();
            var close = new Button { Text = "关闭", Location = new Point(622, 35), Size = new Size(100, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            footer.Controls.AddRange(new Control[] { status, save, close });
            Controls.Add(networks);
            Controls.Add(footer);
            CancelButton = close;
            networks.SelectedIndexChanged += (sender, args) => status.Text = "";
            foreach (var fields in inputs.Values)
                foreach (var field in fields) field.TextChanged += (sender, args) => status.Text = "尚未保存，请点击“保存当前网络”。";
            FormClosing += (sender, args) => {
                if (inputs.Any(pair => !pair.Value.Select(field => field.Text).SequenceEqual(saved[pair.Key])) &&
                    MessageBox.Show(this, "有尚未保存的修改，确定放弃并关闭吗？", "未保存的节点设置",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    args.Cancel = true;
            };
        }

        private void SaveCurrentNetwork()
        {
            var chain = networks.SelectedTab.Name;
            try
            {
                var values = inputs[chain].Select(field => field.Text).ToArray();
                BlockManage.SavePublicNodeUrls(chain, values);
                saved[chain] = values;
                status.Text = chain + " 节点已保存，下一次手动查询生效。";
            }
            catch (ArgumentException ex) { status.Text = ex.Message; }
            catch (Exception) { status.Text = "保存失败，请检查目录权限；本次修改尚未保存。"; }
        }
    }
}
