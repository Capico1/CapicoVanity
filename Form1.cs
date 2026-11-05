using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Util;
using Nethereum.Signer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Text.Json;
using System.Threading;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc;
using System.Net.Sockets;
using NBitcoin.RPC;
using Solana.Unity.Rpc.Messages;
using Nethereum.Web3;
using System.Numerics;
using System.Globalization;
using jmsTools.Manage;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Text.RegularExpressions;
using static NBitcoin.RPC.SignRawTransactionRequest;
using System.Diagnostics;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Newtonsoft.Json.Linq;
using Timer = System.Windows.Forms.Timer;

namespace jmsTools
{
    public partial class Form1 : Form
    {

        private Label nodeStatusLabel;
        private System.Windows.Forms.ToolTip nodeStatusTip;
        private int networkRevision;
        private System.Windows.Forms.ComboBox publicQueryNetwork;
        private System.Windows.Forms.TextBox solPublicAddress;
        private Label solPublicAddressLabel;
        private System.Windows.Forms.ListView foundListView;
        private System.Windows.Forms.TabPage foundTabPage;
        private readonly HashSet<string> recordedHitKeys = new HashSet<string>();
        private string selectedNetwork = "ETH"; // 默认选中的网络

        private volatile bool showPrivateKeys;
        private string generatedAddress;
        private string generatedPrivateKey;
        private System.Windows.Forms.ComboBox vanityEngine;
        private System.Windows.Forms.ComboBox vanityNetwork;
        private string vanityGpuDescription;
        private IntPtr vanitySolGpuDevice;
        private string vanitySolGpuDescription;
        private int activeVanityNetwork;
        private bool changingVanityNetwork;
        private readonly string[][] vanityPatterns = { new[] { "", "" }, new[] { "", "" } };
        private Label vanityGpuStatus;
        private int vanityGpuDevice = -1;
        private bool vanityRunning;
        private string generatedWalletFile;
        private string generatedWalletNetwork;
        private string vanityOutputDirectory = Path.Combine(AppPaths.DataDirectory, "VanityWallets");
        private readonly object pendingNodeStatusLock = new object();
        private bool nodeStatusUpdateQueued;
        private string pendingNodeStatusNetwork;
        private int pendingNodeStatusRevision;
        private BalanceQueryResult pendingNodeStatusResult;
        private bool pendingNodeStatusIsPending;

        //示例（靓号生成功能）ETH地址
        private string originalEthAddress = "0x816e4a1589e363720c15c54dfd2efd16f6377070"; // 原始ETH地址  用于靓号生成功能演示
        private string currentEthAddress; // 当前ETH地址
        public Form1()
        {
            InitializeComponent();
            // 初始化currentEthAddress为originalEthAddress
            currentEthAddress = originalEthAddress;
            UpdateRichTextBox();

            InitializeFoundList();
            InitializeWalletRecovery();
            InitializeVanityGpuControls();
            InitializeVanityWalletList();
            InitializeVanityOptions();
            var revealKeys = new CheckBox { Text = "显示本轮私钥", AutoSize = true, Location = new Point(84, 145) };
            revealKeys.CheckedChanged += (sender, args) => {
                showPrivateKeys = revealKeys.Checked;
                if (generatedAddress != null && !vanityRunning) RefreshGeneratedWallet();
            };
            groupBox1.Controls.Add(revealKeys);
            nodeStatusLabel = new Label {
                AutoEllipsis = true, Location = new Point(17, 113),
                Size = new Size(840, 20), ForeColor = Color.DimGray,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            nodeStatusTip = new System.Windows.Forms.ToolTip(components);
            tabPage2.Controls.Add(nodeStatusLabel);
            label1.Visible = comboBoxMainNet.Visible = false;
            UpdateNodeStatus(selectedNetwork, networkRevision, null, false);
            InitializePublicAddressQuery();
            InitializeTransientRecords();
            var gpuConfigurationPage = new TabPage { Name = "gpuConfigurationPage", Text = "GPU 配置" };
            gpuConfigurationPage.Controls.Add(new GpuConfigurationControl { Dock = DockStyle.Fill });
            tabControl1.TabPages.Add(gpuConfigurationPage);
            InitializeWorkspaceLayout(gpuConfigurationPage);
            FormClosing += (sender, args) => {
                if (cancellationTokenSource != null) cancellationTokenSource.Cancel();
                if (publicQueryCancellation != null) publicQueryCancellation.Cancel();
            };

        }

        private void InitializeWorkspaceLayout(TabPage gpuConfigurationPage)
        {
            SuspendLayout();
            Text = "Capico 靓号";
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(typeof(Form1).Assembly.Location);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1040, 630);
            MinimumSize = new Size(680, 520);
            BackColor = Color.FromArgb(240, 244, 249);
            Controls.Add(new Label {
                Name = "workspaceTitle", Text = "Capico 靓号", AutoSize = true,
                Location = new Point(19, 11), Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 41, 65)
            });
            const string author = "Capico";
            const string authorUrl = "https://x.com/NanaWiti";
            const string subtitle = "ETH / BSC / SOL   ·   离线搜索   ·   私钥加密保存   ·   ";
            var authorLink = new LinkLabel {
                Name = "workspaceAuthorLink", Text = subtitle + author, AutoSize = true,
                Location = new Point(21, 46), ForeColor = Color.FromArgb(67, 85, 108),
                LinkBehavior = LinkBehavior.AlwaysUnderline, TabStop = true
            };
            authorLink.Links.Clear();
            authorLink.Links.Add(subtitle.Length, author.Length, authorUrl);
            Action openAuthor = () => {
                try
                {
                    Process.Start(new ProcessStartInfo(authorUrl) { UseShellExecute = true });
                }
                catch (Win32Exception)
                {
                    MessageBox.Show(this, "无法打开默认浏览器。请手动访问：" + authorUrl,
                        "Capico 靓号", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            authorLink.LinkClicked += (sender, args) => openAuthor();
            nodeStatusTip.SetToolTip(authorLink, "Capico · @NanaWiti · X 主页");
            Controls.Add(authorLink);
            var authorIcon = new System.Windows.Forms.Button {
                Name = "workspaceAuthorIcon", AccessibleName = "在 X 上查看 Capico (@NanaWiti)",
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = Color.Black,
                Size = new Size(18, 18)
            };
            authorIcon.FlatAppearance.BorderSize = 0;
            authorIcon.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 38, 38);
            authorIcon.FlatAppearance.MouseDownBackColor = Color.FromArgb(58, 58, 58);
            Action positionIcon = () => authorIcon.Location = new Point(
                authorLink.Right + 3, authorLink.Top + (authorLink.Height - authorIcon.Height) / 2);
            authorLink.LocationChanged += (sender, args) => positionIcon();
            authorLink.SizeChanged += (sender, args) => positionIcon();
            authorIcon.SizeChanged += (sender, args) => positionIcon();
            positionIcon();
            authorIcon.Paint += (sender, args) => PaintAuthorIcon(args.Graphics, authorIcon.ClientSize);
            nodeStatusTip.SetToolTip(authorIcon, "Capico · @NanaWiti · X 主页");
            authorIcon.Click += (sender, args) => openAuthor();
            Controls.Add(authorIcon);
            tabControl1.SetBounds(16, 74, ClientSize.Width - 32, ClientSize.Height - 90);
            tabControl1.Padding = new Point(12, 7);
            var recoveryPage = tabControl1.TabPages["walletRecoveryTab"];
            tabControl1.TabPages.Clear();
            tabControl1.TabPages.AddRange(new[] { tabPage3, savedVanityTab, tabPage2, recoveryPage, foundTabPage, gpuConfigurationPage });
            tabControl1.SelectedTab = tabPage3;
            ResumeLayout(true);
        }

        private static void PaintAuthorIcon(Graphics graphics, Size size)
        {
            var state = graphics.Save();
            try
            {
                float scale = Math.Min(size.Width, size.Height) / 32f;
                graphics.TranslateTransform((size.Width - 24 * scale) / 2, (size.Height - 24 * scale) / 2);
                graphics.ScaleTransform(scale, scale);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // The inner contour leaves the wide diagonal hollow at small sizes.
                using (var shape = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    shape.AddPolygon(new[] {
                        new PointF(14.28f, 10.16f), new PointF(23.21f, 0), new PointF(21.09f, 0),
                        new PointF(13.34f, 8.82f), new PointF(7.14f, 0), new PointF(0, 0),
                        new PointF(9.36f, 13.32f), new PointF(0, 24), new PointF(2.12f, 24),
                        new PointF(10.30f, 14.65f), new PointF(16.86f, 24), new PointF(24, 24)
                    });
                    shape.AddPolygon(new[] {
                        new PointF(2.88f, 1.56f), new PointF(6.14f, 1.56f),
                        new PointF(21.10f, 22.51f), new PointF(17.85f, 22.51f)
                    });
                    graphics.FillPath(Brushes.White, shape);
                }
            }
            finally { graphics.Restore(state); }
        }

        private void InitializeFoundList()
        {
            var foundTab = new TabPage { Text = "命中列表", Name = "foundTab" };
            foundTabPage = foundTab;
            foundListView = new System.Windows.Forms.ListView {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Scrollable = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                ContextMenuStrip = BuildFoundListMenu()
            };
            foundListView.Columns.Add("时间", 140);
            foundListView.Columns.Add("网络", 50);
            foundListView.Columns.Add("余额", 100);
            foundListView.Columns.Add("地址", 300);
            foundListView.Columns.Add("来源", 70);
            foundListView.MouseDoubleClick += FoundListDoubleClick;
            foundListView.KeyDown += FoundListKeyDown;

            var toolbar = new System.Windows.Forms.Panel { Dock = DockStyle.Top, Height = 34 };
            var copyButton = new System.Windows.Forms.Button { Text = "复制公开信息", Location = new Point(8, 5), Size = new Size(108, 26) };
            copyButton.Click += (sender, args) => CopyFoundRow();
            toolbar.Controls.Add(copyButton);
            toolbar.Controls.Add(new Label {
                Text = "私钥和助记词已隐藏；右键选中记录可单独复制。",
                AutoSize = true, Location = new Point(126, 10)
            });
            foundTab.Controls.Add(foundListView);
            foundTab.Controls.Add(toolbar);
            tabControl1.Controls.Add(foundTab);
        }

        private void InitializeWalletRecovery()
        {
            var page = new TabPage { Name = "walletRecoveryTab", Text = "找回自己的钱包", AutoScroll = true };
            var recovery = new MnemonicRecoveryForm(
                found => RecordFoundWallet(found.Network, found.Address, found.PrivateKey, found.Mnemonic, 0, "恢复"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wordlist.txt"),
                () => tabControl1.SelectedTab = foundTabPage) {
                TopLevel = false, FormBorderStyle = FormBorderStyle.None,
                MinimumSize = new Size(0, 440), Dock = DockStyle.Fill
            };
            page.Controls.Add(recovery);
            tabControl1.TabPages.Add(page);
            recovery.Show();
            FormClosing += (sender, args) => {
                if (recovery.StopBeforeClosing()) args.Cancel = true;
            };
        }

        private System.Windows.Forms.ContextMenuStrip BuildFoundListMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            var copyAddress = menu.Items.Add("复制地址", null, (sender, args) => CopyFoundAddress());
            var copyPrivateKey = menu.Items.Add("复制私钥…", null, (sender, args) => CopyFoundSecret(false));
            var copyMnemonic = menu.Items.Add("复制助记词…", null, (sender, args) => CopyFoundSecret(true));
            var copyPublicRow = menu.Items.Add("复制公开信息", null, (sender, args) => CopyFoundRow());
            menu.Opening += (sender, args) => {
                var item = SelectedFoundItem();
                var secrets = item?.Tag as FoundWalletSecrets;
                copyAddress.Enabled = copyPublicRow.Enabled = item != null;
                copyPrivateKey.Enabled = !string.IsNullOrWhiteSpace(secrets?.PrivateKey);
                copyMnemonic.Enabled = !string.IsNullOrWhiteSpace(secrets?.Mnemonic);
            };
            return menu;
        }

        private void FoundListDoubleClick(object sender, MouseEventArgs e)
        {
            if (foundListView == null) return;
            var hit = foundListView.HitTest(e.Location);
            if (hit.Item != null && hit.SubItem != null)
                CopyFoundPublicText(hit.SubItem.Text);
        }

        private void FoundListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopyFoundRow();
                e.Handled = true;
            }
        }

        private System.Windows.Forms.ListViewItem SelectedFoundItem()
        {
            return foundListView == null || foundListView.SelectedItems.Count == 0 ? null : foundListView.SelectedItems[0];
        }

        private void CopyFoundAddress()
        {
            var item = SelectedFoundItem();
            if (item != null) CopyFoundPublicText(item.SubItems[3].Text);
        }

        private void CopyFoundRow()
        {
            var item = SelectedFoundItem();
            if (item == null) return;
            CopyFoundPublicText(FormatFoundPublicRow(item));
        }

        private static string FormatFoundPublicRow(System.Windows.Forms.ListViewItem item) =>
            string.Join("\t", item.SubItems[0].Text, item.SubItems[1].Text,
                item.SubItems[2].Text, item.SubItems[3].Text, item.SubItems[4].Text);

        private void CopyFoundPublicText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try { Clipboard.SetText(text); }
            catch (System.Runtime.InteropServices.ExternalException) { ShowClipboardFailure(); }
        }

        private void CopyFoundSecret(bool mnemonic)
        {
            var secrets = SelectedFoundItem()?.Tag as FoundWalletSecrets;
            var value = mnemonic ? secrets?.Mnemonic : secrets?.PrivateKey;
            if (!string.IsNullOrWhiteSpace(value))
                ConfirmAndCopyWalletSecret(mnemonic ? "助记词" : "私钥", () => value);
        }

        private bool ConfirmAndCopyWalletSecret(string label, Func<string> readValue)
        {
            if (MessageBox.Show(this,
                "复制" + label + "后，其他程序仍可能读取剪贴板。请只粘贴到可信的钱包，用完后清除剪贴板。\r\n\r\n要继续复制吗？",
                "复制" + label, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
                != DialogResult.Yes) return false;
            try
            {
                CopyWalletSecretToClipboard(readValue());
                return true;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                ShowClipboardFailure();
                return false;
            }
        }

        private static void CopyWalletSecretToClipboard(string value)
        {
            // This registered format excludes the item from Windows history and cloud sync.
            using (var exclusion = new MemoryStream(new byte[] { 1 }))
            {
                var data = CreateWalletSecretClipboardData(value, exclusion);
                Clipboard.SetDataObject(data, true, 5, 50);
            }
        }

        private static System.Windows.Forms.DataObject CreateWalletSecretClipboardData(string value, Stream exclusion)
        {
            var data = new System.Windows.Forms.DataObject();
            data.SetText(value, TextDataFormat.UnicodeText);
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", false, exclusion);
            return data;
        }

        private void ShowClipboardFailure()
        {
            MessageBox.Show(this, "无法写入剪贴板，请稍后重试。", "复制失败",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private sealed class FoundWalletSecrets
        {
            internal readonly string PrivateKey;
            internal readonly string Mnemonic;

            internal FoundWalletSecrets(string privateKey, string mnemonic)
            {
                PrivateKey = privateKey;
                Mnemonic = mnemonic;
            }
        }

        private void RecordFoundWallet(string network, string address, string privateKey, string mnemonic, decimal balance, string source)
        {
            if (foundListView == null || IsDisposed || Disposing) return;
            if (foundListView.InvokeRequired)
            {
                foundListView.BeginInvoke(new Action(() => RecordFoundWallet(network, address, privateKey, mnemonic, balance, source)));
                return;
            }

            var hitKey = network + ":" + address;
            if (!recordedHitKeys.Add(hitKey)) return;

            var item = new System.Windows.Forms.ListViewItem(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(network);
            item.SubItems.Add(balance.ToString("0.##################"));
            item.SubItems.Add(address);
            item.SubItems.Add(source);
            item.Tag = new FoundWalletSecrets(privateKey, mnemonic);
            foundListView.Items.Insert(0, item);
            while (foundListView.Items.Count > 5000)
            {
                var removed = foundListView.Items[foundListView.Items.Count - 1];
                recordedHitKeys.Remove(removed.SubItems[1].Text + ":" + removed.SubItems[3].Text);
                foundListView.Items.RemoveAt(foundListView.Items.Count - 1);
            }
            if (foundTabPage != null)
                foundTabPage.Text = "命中列表 (" + foundListView.Items.Count + ")";
        }

        private void InitializePublicAddressQuery()
        {
            publicQueryNetwork = new System.Windows.Forms.ComboBox {
                Name = "publicQueryNetwork", DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(90, 16), Size = new Size(240, 21)
            };
            publicQueryNetwork.Items.AddRange(new object[] { "ETH", "BSC", "SOL", "全部网络（ETH / BSC / SOL）" });
            tabPage2.Controls.Add(new Label { Text = "查询网络：", AutoSize = true, Location = new Point(17, 20) });
            tabPage2.Controls.Add(publicQueryNetwork);
            var publicNodeSettings = new System.Windows.Forms.Button {
                Name = "publicNodeSettings", Text = "查询节点设置",
                Location = new Point(346, 14), Size = new Size(112, 25)
            };
            publicNodeSettings.Click += (sender, args) => {
                try {
                    using (var settings = new PublicNodeSettingsForm(publicQueryNetwork.SelectedIndex < 3
                        ? publicQueryNetwork.SelectedItem.ToString() : "ETH")) settings.ShowDialog(this);
                }
                catch (Exception) {
                    MessageBox.Show("无法读取查询节点配置，请检查目录权限。", "查询节点设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            tabPage2.Controls.Add(publicNodeSettings);
            label2.Location = new Point(17, 54);
            oneaddress.Location = new Point(130, 50);
            oneaddress.Size = new Size(570, 21);
            oneaddress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            solPublicAddressLabel = new Label { Text = "SOL 公开地址：", AutoSize = true, Location = new Point(17, 86) };
            solPublicAddress = new System.Windows.Forms.TextBox {
                Name = "solPublicAddress", Location = new Point(130, 82), Size = new Size(570, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabPage2.Controls.Add(solPublicAddressLabel);
            tabPage2.Controls.Add(solPublicAddress);
            label3.Text = "查询结果：";
            label3.Location = new Point(17, 140);
            BalanceInfo.Location = new Point(90, 140);
            BalanceInfo.Text = "等待查询（原生币）";
            text_transaction.Location = new Point(17, 178);
            text_transaction.ReadOnly = true;
            text_transaction.ScrollBars = ScrollBars.Vertical;
            oneaddress_cx.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            oneaddress_cx.Text = "查询";
            publicQueryNetwork.SelectedIndexChanged += (sender, args) => {
                bool all = publicQueryNetwork.SelectedIndex == 3;
                if (publicQueryNetwork.SelectedIndex >= 0)
                    comboBoxMainNet.SelectedIndex = all ? 0 : publicQueryNetwork.SelectedIndex;
                label2.Text = all ? "ETH / BSC 地址：" : "钱包公开地址：";
                solPublicAddress.Visible = all;
                solPublicAddressLabel.Visible = all;
            };
            publicQueryNetwork.SelectedIndex = 3;
        }

        private void UpdateRichTextBox()
        {
            xample_str.Text = currentEthAddress;
            xample_str.SelectAll();
            xample_str.SelectionColor = Color.Black; // 恢复默认颜色

            string prefix = textBoxPrefix.Text;
            int prefixLength = Math.Min(prefix.Length, 6);
            if (prefixLength > 0)
            {
                xample_str.SelectionStart = 2;
                xample_str.SelectionLength = prefixLength;
                xample_str.SelectionColor = Color.Red; // 高亮颜色
            }

            string suffix = textBoxSuffix.Text;
            int suffixLength = suffix.Length;
            if (suffixLength > 0)
            {
                xample_str.SelectionStart = currentEthAddress.Length - suffixLength;
                xample_str.SelectionLength = suffixLength;
                xample_str.SelectionColor = Color.Red; // 高亮颜色
            }
        }


        private void UpdateNodeStatus(string network, int revision, BalanceQueryResult result, bool pending)
        {
            if (IsDisposed || Disposing) return;
            if (revision != networkRevision || network != selectedNetwork) return;
            if (InvokeRequired)
            {
                lock (pendingNodeStatusLock)
                {
                    pendingNodeStatusNetwork = network;
                    pendingNodeStatusRevision = revision;
                    pendingNodeStatusResult = result;
                    pendingNodeStatusIsPending = pending;
                    if (nodeStatusUpdateQueued) return;
                    nodeStatusUpdateQueued = true;
                }
                try { BeginInvoke(new Action(ApplyPendingNodeStatus)); }
                catch (InvalidOperationException) { }
                return;
            }
            string state = pending ? "查询中" : result == null ? "未检测" : result.Success ? "正常（最近查询）" : "异常（最近查询）";
            nodeStatusLabel.Text = $"{network} 节点：{state}" +
                (result == null ? "" : $"\n更新：{DateTime.Now:HH:mm:ss}");
            nodeStatusLabel.ForeColor = pending ? Color.DarkOrange : result == null ? Color.DimGray : result.Success ? Color.ForestGreen : Color.Firebrick;
            nodeStatusTip.SetToolTip(nodeStatusLabel, result == null ? "执行查询后更新节点状态" : result.Success ? "最近一次余额查询成功；状态不代表持续在线" : result.Error);
        }

        private void ApplyPendingNodeStatus()
        {
            string network;
            int revision;
            BalanceQueryResult result;
            bool pending;
            lock (pendingNodeStatusLock)
            {
                network = pendingNodeStatusNetwork;
                revision = pendingNodeStatusRevision;
                result = pendingNodeStatusResult;
                pending = pendingNodeStatusIsPending;
                nodeStatusUpdateQueued = false;
            }
            if (IsDisposed || Disposing) return;
            UpdateNodeStatus(network, revision, result, pending);
        }

        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {


            comboBoxMainNet.SelectedIndex = 0; // 默认选中第一个项
            selectedNetwork = comboBoxMainNet.SelectedItem.ToString();
        }

        /// <summary>
        /// 主网切换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxMainNet_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxMainNet.SelectedItem != null)
            {
                selectedNetwork = comboBoxMainNet.SelectedItem.ToString();
                networkRevision++;
                UpdateNodeStatus(selectedNetwork, networkRevision, null, false);
            }
        }


        /// <summary>
        /// 单地址查询
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void oneaddress_cx_Click(object sender, EventArgs e)
        {
            var network = publicQueryNetwork.SelectedIndex == 3 ? "ALL" : publicQueryNetwork.SelectedItem.ToString();
            try
            {
                await QueryPublicAddressesAsync(oneaddress.Text, solPublicAddress.Text, network);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "地址格式错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }


        public static long GetTimeStamp(bool accurateToMilliseconds = false)
        {
            if (accurateToMilliseconds)
            {
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            else
            {
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        /// <summary>
        /// 单地址余额查询
        /// </summary>
        /// <param name="address"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        public async Task SomeMethod(string address, string network)
        {
            await QueryPublicAddressesAsync(address, "", network);
        }

        private static List<KeyValuePair<string, string>> PublicAddressRequests(string address, string solAddress, string network)
        {
            address = (address ?? "").Trim();
            solAddress = (solAddress ?? "").Trim();
            var networks = network == "ALL" ? new[] { "ETH", "BSC", "SOL" } : new[] { network };
            var requests = new List<KeyValuePair<string, string>>();
            foreach (var chain in networks)
            {
                if (chain != "ETH" && chain != "BSC" && chain != "SOL") throw new ArgumentException("不支持的查询网络。");
                var value = network == "ALL" && chain == "SOL" ? solAddress : address;
                if (value.Length > 0)
                {
                    bool valid;
                    if (chain == "SOL")
                    {
                        try { valid = NBitcoin.DataEncoders.Encoders.Base58.DecodeData(value).Length == 32; }
                        catch (FormatException) { valid = false; }
                    }
                    else valid = Regex.IsMatch(value, @"\A0x[0-9a-fA-F]{40}\z");
                    if (!valid) throw new ArgumentException(chain + " 公开地址格式不正确；请填写公开地址，不要填写私钥或助记词。");
                }
                requests.Add(new KeyValuePair<string, string>(chain, value));
            }
            if (requests.All(request => request.Value.Length == 0)) throw new ArgumentException("请至少填写一个公开地址。ETH / BSC 共用一个地址，SOL 需单独填写。");
            return requests;
        }

        private static string FormatPublicQueryResult(string network, string address, BalanceQueryResult result)
        {
            var symbol = network == "BSC" ? "BNB" : network;
            return network + "（" + symbol + "）\r\n地址：" + address + "\r\n" +
                (result.Success ? "状态：查询成功\r\n余额：" + result.Balance.ToString("0.##################") + " " + symbol : "状态：查询失败\r\n余额：未知\r\n原因：" + result.Error) +
                (string.IsNullOrEmpty(result.NodeDiagnostics) ? "" : "\r\n线路：" + result.NodeDiagnostics);
        }

        private async Task QueryPublicAddressesAsync(string address, string solAddress, string network)
        {
            if (!oneaddress_cx.Enabled) return;
            var requests = PublicAddressRequests(address, solAddress, network);
            var revision = networkRevision;
            var cts = new CancellationTokenSource();
            publicQueryCancellation = cts;
            cancelPublicQuery.Enabled = true;
            oneaddress_cx.Enabled = false;
            publicQueryNetwork.Enabled = false;
            oneaddress.ReadOnly = true;
            solPublicAddress.ReadOnly = true;
            publicSucceeded = publicFailed = publicSkipped = publicTimedOut = 0;
            publicQueryState = "查询中";
            publicQueryWatch.Restart();
            publicDisplayRecords.Clear();
            publicQueryPending = null;
            UpdatePublicQuerySummary();
            text_transaction.Clear();
            try
            {
                foreach (var request in requests)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    if (request.Value.Length == 0)
                    {
                        publicSkipped++;
                        AddPublicQueryRecord(request.Key + "：未填写公开地址，已跳过。", true);
                        UpdatePublicQuerySummary();
                        continue;
                    }
                    RenderPublicQueryRecords(request.Key + "：查询中...");
                    UpdateNodeStatus(request.Key, revision, null, true);
                    var requestWatch = Stopwatch.StartNew();
                    var result = await publicBalanceQuery(request.Value, request.Key, cts.Token);
                    cts.Token.ThrowIfCancellationRequested();
                    if (IsDisposed || Disposing) return;
                    UpdateNodeStatus(request.Key, revision, result, false);
                    RecordPublicQueryOutcome(result.Success, result.Error + " " + result.NodeDiagnostics);
                    AddPublicQueryRecord(FormatPublicQueryResult(request.Key, request.Value, result) +
                        "\r\n请求用时：" + requestWatch.Elapsed.TotalSeconds.ToString("0.00") + " 秒", !result.Success);
                }

                cts.Token.ThrowIfCancellationRequested();
                publicQueryState = "已完成";
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                publicQueryState = "已取消";
                if (!IsDisposed && !Disposing) {
                    AddPublicQueryRecord("查询已取消。", true);
                    UpdateNodeStatus(selectedNetwork, revision, null, false);
                }
            }
            catch (Exception)
            {
                publicQueryState = "查询失败";
                if (!IsDisposed && !Disposing) {
                    RecordPublicQueryOutcome(false, "查询异常");
                    AddPublicQueryRecord("查询失败，请检查网络与节点配置。", true);
                    UpdateNodeStatus(selectedNetwork, revision, BalanceQueryResult.Failed("查询失败，请检查网络与节点配置。"), false);
                }
            }
            finally
            {
                publicQueryWatch.Stop();
                publicQueryCancellation = null;
                cts.Dispose();
                if (!IsDisposed && !Disposing)
                {
                    cancelPublicQuery.Enabled = false;
                    publicQueryPending = null;
                    RenderPublicQueryRecords();
                    UpdatePublicQuerySummary();
                    oneaddress_cx.Enabled = true;
                    publicQueryNetwork.Enabled = true;
                    oneaddress.ReadOnly = false;
                    solPublicAddress.ReadOnly = false;
                }
            }
        }

        /// <summary>
        /// 前缀
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private bool IsSolVanity { get { return vanityNetwork != null && vanityNetwork.SelectedIndex == 1; } }

        private void textBoxPrefix_TextChanged(object sender, EventArgs e) { RefreshVanityPattern(); }
        private void textBoxSuffix_TextChanged(object sender, EventArgs e) { RefreshVanityPattern(); }

        private string ValidateVanityPattern(string requestedPrefix, string requestedSuffix)
        {
            if (IsSolVanity) return SolanaVanityGenerator.ValidatePattern(requestedPrefix, requestedSuffix, checkBox1.Checked);
            if (!Regex.IsMatch(requestedPrefix, "^[0-9a-fA-F]*$") ||
                !Regex.IsMatch(requestedSuffix, "^[0-9a-fA-F]*$") || requestedPrefix.Length + requestedSuffix.Length > 40)
                return "EVM 前后缀只能包含 0-9、a-f，合计不超过 40 位（不含 0x）。";
            return null;
        }

        private void RefreshVanityPattern()
        {
            if (changingVanityNetwork) return;
            if (RefreshVanityTemplatePreview()) return;
            string requestedPrefix = textBoxPrefix.Text.Trim();
            string requestedSuffix = textBoxSuffix.Text.Trim();
            string error = ValidateVanityPattern(requestedPrefix, requestedSuffix);
            if (error != null) { xample_str.Text = error; complexity_lab.Text = probability_lab.Text = "—"; ygtime.Text = "请检查前后缀"; return; }
            string sample = IsSolVanity ? "7YttLkHDoNj9wyDur5iKf9TSPfNE7pExvKjV9EY4Q5qi" : originalEthAddress.Substring(2);
            int length = sample.Length;
            string value = requestedPrefix + sample.Substring(Math.Min(requestedPrefix.Length, length),
                Math.Max(0, length - requestedPrefix.Length - requestedSuffix.Length)) + requestedSuffix;
            xample_str.Text = (IsSolVanity ? "" : "0x") + value;
            RefreshVanityEstimate(VanityEstimate.ExpectedAttempts(IsSolVanity, checkBox1.Checked, requestedPrefix, requestedSuffix, null));
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // 打开默认浏览器并导航到指定网址
            Process.Start(new ProcessStartInfo("https://bqbot.cn") { UseShellExecute = true });
        }


        // 线程安全的计数器
        private long totalGeneratedCount = 0;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// 地址生成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InitializeVanityGpuControls()
        {
            foreach (Control control in 生成配置.Controls) control.Top += 32;
            vanityNetwork = new System.Windows.Forms.ComboBox {
                Name = "vanityNetwork", DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(88, 25), Size = new Size(335, 21)
            };
            vanityNetwork.Items.AddRange(new object[] { "EVM（ETH / BSC）", "SOL（Solana）" });
            vanityNetwork.SelectedIndex = 0;
            生成配置.Controls.Add(new Label { Text = "钱包链：", AutoSize = true, Location = new Point(41, 29) });
            生成配置.Controls.Add(vanityNetwork);
            vanityEngine = new System.Windows.Forms.ComboBox {
                Name = "vanityEngine", DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(269, 130), Size = new Size(154, 21)
            };
            vanityGpuDescription = VanityGpuGenerator.Detect(out vanityGpuDevice);
            vanitySolGpuDescription = SolanaVanityGpuGenerator.Detect(out vanitySolGpuDevice);
            vanityEngine.Items.AddRange(new object[] { "自动（GPU 优先）", "仅 CPU" });
            vanityEngine.SelectedIndex = 0;
            生成配置.Controls.Add(new Label { Text = "计算引擎：", AutoSize = true, Location = new Point(199, 134) });
            生成配置.Controls.Add(vanityEngine);
            vanityGpuStatus = new Label {
                Name = "vanityGpuStatus", Text = vanityGpuDescription, AutoEllipsis = true,
                Location = new Point(43, 203), Size = new Size(385, 32), ForeColor = Color.DimGray
            };
            生成配置.Controls.Add(vanityGpuStatus);
            InitializeVanityBatchControls();
            vanityEngine.SelectedIndexChanged += (sender, args) => {
                RefreshVanityBatchControls();
                UpdateVanityEngineStatus();
                RefreshVanityPattern();
            };
            label7.Visible = false;
            checkBox1.CheckedChanged += (sender, args) => RefreshVanityPattern();
            vanityNetwork.SelectedIndexChanged += (sender, args) => {
                if (vanityRunning) return;
                vanityPatterns[activeVanityNetwork][0] = textBoxPrefix.Text;
                vanityPatterns[activeVanityNetwork][1] = textBoxSuffix.Text;
                activeVanityNetwork = vanityNetwork.SelectedIndex;
                changingVanityNetwork = true;
                textBoxPrefix.Text = vanityPatterns[activeVanityNetwork][0];
                textBoxSuffix.Text = vanityPatterns[activeVanityNetwork][1];
                changingVanityNetwork = false;
                UpdateVanityEngineStatus();
                RefreshVanityBatchControls();
                RefreshVanityPattern();
            };
            UpdateVanityEngineStatus();
            RefreshVanityPattern();
            stop_btn.Enabled = false;
        }

        private bool VanityGpuAvailable => IsSolVanity ? vanitySolGpuDevice != IntPtr.Zero : vanityGpuDevice >= 0;
        private bool PreferVanityGpu => vanityEngine?.SelectedIndex == 0 && VanityGpuAvailable;

        private void UpdateVanityEngineStatus()
        {
            string device = IsSolVanity ? vanitySolGpuDescription : vanityGpuDescription;
            vanityGpuStatus.Text = PreferVanityGpu ? "GPU 优先 · " + device
                : vanityEngine.SelectedIndex == 1 ? "使用多线程 CPU"
                : "未检测到兼容 GPU，将使用 CPU · " + device;
        }

        private async void generate_btn_Click(object sender, EventArgs e)
        {
            if (vanityRunning) return;
            VanityTemplate requestedTemplate;
            VanityTemplate[] requestedTemplates;
            try { requestedTemplate = RequestedVanityTemplate(); requestedTemplates = RequestedVanityTemplates(requestedTemplate); }
            catch (ArgumentException error) { labelResults.Text = error.Message; return; }
            string requestedPrefix = requestedTemplate == null ? textBoxPrefix.Text.Trim() : "";
            string requestedSuffix = requestedTemplate == null ? textBoxSuffix.Text.Trim() : "";
            string patternError = ValidateVanityPattern(requestedPrefix, requestedSuffix);
            if (patternError != null) { labelResults.Text = patternError; return; }
            bool isSol = IsSolVanity;
            bool useGpu = PreferVanityGpu;
            bool gpuFailed = false;
            string engine = useGpu ? "GPU" : "CPU";
            bool matchCase = checkBox1.Checked;
            int requestedSolBatch = vanityBatchSettings.SolBatchSize;
            uint requestedEvmBatch = vanityBatchSettings.EvmBatchMultiple;
            int requestedStopMode = vanityStopMode.SelectedIndex;
            int requestedCount = requestedStopMode == 0 ? 1 : (int)vanityTargetCount.Value;
            long targetCount = requestedStopMode == 2 ? long.MaxValue : requestedCount;
            double expectedAttempts = requestedTemplate == null ? VanityEstimate.ExpectedAttempts(isSol, matchCase, requestedPrefix, requestedSuffix, null)
                : VanityEstimate.ExpectedTemplateSet(isSol, matchCase, requestedTemplates);
            bool multipleTemplates = requestedTemplates.Length > 1;
            int activeEvmTemplate = 0;
            int nextEvmTemplate = 0;
            string runId = Guid.NewGuid().ToString("N");
            SaveVanityOptions();
            var cts = new CancellationTokenSource();
            cancellationTokenSource = cts;
            vanityRunning = true;
            vanityMatchesFound = 0;
            UpdateVanityFoundCount();
            generatedAddress = null;
            generatedPrivateKey = null;
            generatedWalletFile = null;
            generatedWalletNetwork = null;
            Interlocked.Exchange(ref totalGeneratedCount, 0);
            UpdateGeneratedCountLabel();
            string preparing = !useGpu ? (vanityEngine.SelectedIndex == 0 ? "未检测到兼容 GPU，正在准备 CPU…" : "正在准备多线程 CPU…") :
                isSol ? "正在准备 SOL GPU，首次运行会自动优化参数…" :
                requestedEvmBatch == 0 ? "正在准备 EVM GPU，首次运行会自动优化参数…" : "正在初始化 GPU…";
            labelResults.Text = preparing;
            SetVanityControlsRunning(true);
            var watch = Stopwatch.StartNew();
            var progressTimer = new Timer { Interval = 250 };
            long searchRate = 0;
            progressTimer.Tick += (tickSender, tickArgs) => {
                UpdateGeneratedCountLabel();
                double rate = Interlocked.Read(ref searchRate);
                vanityGpuStatus.Text = (isSol ? "SOL " : "EVM ") + engine + (gpuFailed ? "（GPU 回退）" : "") + "  ·  " +
                    (rate >= 1e8 ? (rate / 1e8).ToString("0.##") + " 亿" : rate >= 1e4 ? (rate / 1e4).ToString("0.##") + " 万" : rate.ToString("N0")) +
                    " 地址/秒  ·  " + watch.Elapsed.ToString(@"hh\:mm\:ss");
                if (rate > 0)
                {
                    if (labelResults.Text == preparing) labelResults.Text = engine + " 正在搜索靓号…";
                    if (useGpu && !isSol && multipleTemplates) vanityGpuStatus.Text += " · " + requestedTemplates[Volatile.Read(ref activeEvmTemplate)].Pattern;
                    if (!cts.IsCancellationRequested)
                        ShowVanityEstimateTime(expectedAttempts, requestedStopMode, targetCount - vanityMatchesFound, rate);
                }
            };
            void SwitchToCpu()
            {
                cts.Token.ThrowIfCancellationRequested();
                useGpu = false;
                gpuFailed = true;
                engine = "CPU";
                Interlocked.Exchange(ref searchRate, 0);
                if (IsDisposed || Disposing) return;
                preparing = "GPU 无法运行，本轮已自动切换到 CPU…";
                labelResults.Text = preparing;
                ShowVanityEstimateTime(expectedAttempts, requestedStopMode, targetCount - vanityMatchesFound, 0);
            }
            progressTimer.Start();
            try
            {
                if (useGpu && !isSol && requestedEvmBatch == 0)
                {
                    try
                    {
                        requestedEvmBatch = await Task.Run(() => VanityGpuGenerator.ResolveAutomaticBatch(vanityGpuDevice, cts.Token));
                        if (!IsDisposed && !Disposing)
                        {
                            preparing = "EVM 自动优化：已选择 " + requestedEvmBatch.ToString("N0") + " 倍批次，正在初始化…";
                            labelResults.Text = preparing;
                        }
                    }
                    catch (Exception error) when (VanityGpuGenerator.IsGpuFailure(error)) { SwitchToCpu(); }
                }
                long completedAttempts = 0;
                while (vanityMatchesFound < targetCount)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    long attemptsThisResult = 0;
                    Action<long, ulong> progress = (count, rate) => {
                        attemptsThisResult = count;
                        Interlocked.Exchange(ref totalGeneratedCount, completedAttempts + count);
                        if (rate > 0) Interlocked.Exchange(ref searchRate, (long)rate);
                    };
                    VanityGpuGenerator.Result result;
                    try
                    {
                        result = await Task.Run(() => !useGpu
                            ? GenerateVanityCpu(requestedPrefix, requestedSuffix, matchCase, isSol, requestedTemplates, cts.Token, progress)
                            : isSol
                        ? SolanaVanityGpuGenerator.Generate(requestedPrefix, requestedSuffix, matchCase, vanitySolGpuDevice, cts.Token,
                            progress, requestedSolBatch, null, -1, requestedTemplate, multipleTemplates ? requestedTemplates.Skip(1).ToArray() : null)
                        : requestedTemplate == null
                            ? VanityGpuGenerator.Generate(requestedPrefix, requestedSuffix, matchCase, vanityGpuDevice, cts.Token,
                                progress, status => { }, requestedEvmBatch)
                            : multipleTemplates ? VanityGpuGenerator.GenerateTemplateSet(requestedTemplates, matchCase, vanityGpuDevice, cts.Token,
                                progress, index => Volatile.Write(ref activeEvmTemplate, index), nextEvmTemplate, requestedEvmBatch)
                            : VanityGpuGenerator.GenerateTemplate(requestedTemplate, matchCase, vanityGpuDevice, cts.Token,
                                progress, status => { }, requestedEvmBatch));
                    }
                    catch (Exception error) when (useGpu && VanityGpuGenerator.IsGpuFailure(error))
                    {
                        completedAttempts += attemptsThisResult;
                        SwitchToCpu();
                        continue;
                    }
                    completedAttempts += attemptsThisResult;
                    if (useGpu && !isSol && multipleTemplates) nextEvmTemplate = (Volatile.Read(ref activeEvmTemplate) + 1) % requestedTemplates.Length;
                    if (IsDisposed || Disposing) return;
                    // A completed, verified result is saved even if stop was clicked
                    // while its task completion was waiting for the UI thread.
                    generatedAddress = result.Address;
                    generatedPrivateKey = result.PrivateKey;
                    generatedWalletFile = null;
                    generatedWalletNetwork = isSol ? "SOL" : "EVM（ETH / BSC）";
                    labelResults.Text = "已找到靓号，正在保存…";
                    try
                    {
                        generatedWalletFile = await Task.Run(() => SaveVanityWallet(result, requestedPrefix, requestedSuffix, matchCase, useGpu, isSol,
                            requestedTemplate, requestedStopMode, requestedCount, runId, vanityMatchesFound + 1, requestedTemplates));
                        generatedPrivateKey = null;
                        result.PrivateKey = null;
                    }
                    catch (Exception saveError)
                    {
                        if (!IsDisposed && !Disposing)
                        {
                            RefreshGeneratedWallet();
                            labelResults.AppendText(Environment.NewLine + "自动保存失败：" + saveError.Message + Environment.NewLine + "生成已停止，请复制并保管本次结果。");
                            vanityGpuStatus.Text = "已找到并停止 · 保存失败";
                        }
                        return;
                    }
                    if (IsDisposed || Disposing) return;
                    vanityMatchesFound++;
                    UpdateVanityFoundCount();
                    if (vanityMatchesFound < targetCount && !cts.IsCancellationRequested)
                        ShowVanityEstimateTime(expectedAttempts, requestedStopMode, targetCount - vanityMatchesFound, Interlocked.Read(ref searchRate));
                    AddSavedVanityWallet(generatedWalletFile);
                    RefreshGeneratedWallet();
                    if (vanityMatchesFound >= targetCount)
                    {
                        labelResults.AppendText(Environment.NewLine + "本轮已保存 " + vanityMatchesFound + " 个，已达到停止条件。");
                        vanityGpuStatus.Text = engine + " 已保存并停止 · " + vanityMatchesFound + " 个 · " + watch.Elapsed.TotalSeconds.ToString("0.00") + " 秒";
                        break;
                    }
                    cts.Token.ThrowIfCancellationRequested();
                    labelResults.AppendText(Environment.NewLine + "本轮已保存 " + vanityMatchesFound + " 个，继续寻找下一个…");
                    // Yield between saves so even very easy patterns remain stoppable.
                    await Task.Delay(15, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !Disposing) {
                    if (generatedAddress != null) RefreshGeneratedWallet(); else labelResults.Clear();
                    labelResults.AppendText(Environment.NewLine + "任务已手动停止，本轮已保存 " + vanityMatchesFound + " 个。");
                    vanityGpuStatus.Text = "已停止 · 已保存 " + vanityMatchesFound + " 个";
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing) {
                    if (generatedAddress != null) RefreshGeneratedWallet(); else labelResults.Clear();
                    labelResults.AppendText(Environment.NewLine + "生成失败：" + ex.Message + "；本轮已保存 " + vanityMatchesFound + " 个。");
                    vanityGpuStatus.Text = engine + " 运行失败";
                }
            }
            finally
            {
                progressTimer.Stop();
                progressTimer.Dispose();
                watch.Stop();
                vanityRunning = false;
                if (ReferenceEquals(cancellationTokenSource, cts)) cancellationTokenSource = null;
                bool stopped = cts.IsCancellationRequested;
                cts.Dispose();
                if (!IsDisposed && !Disposing)
                {
                    long rate = Interlocked.Read(ref searchRate);
                    if (rate > 0) vanityEstimateRates[VanityRateIndex(isSol, requestedTemplate != null, useGpu)] = rate;
                    UpdateGeneratedCountLabel();
                    SetVanityControlsRunning(false);
                    ygtime.Text = vanityMatchesFound >= targetCount ? "已完成 · 实际 " + FormatVanityTime(watch.Elapsed.TotalSeconds)
                        : stopped ? "已停止 · 本轮 " + FormatVanityTime(watch.Elapsed.TotalSeconds) : "本轮未完成";
                }
            }
        }

        private void SetVanityControlsRunning(bool running)
        {
            generate_btn.Enabled = !running;
            stop_btn.Enabled = running;
            vanityEngine.Enabled = vanityNetwork.Enabled = !running;
            textBoxPrefix.Enabled = textBoxSuffix.Enabled = checkBox1.Enabled = !running;
            vanityBatch.Enabled = saveVanityBatch.Enabled = !running && PreferVanityGpu;
            RefreshVanityOptions();
        }

        private static VanityGpuGenerator.Result GenerateVanityCpu(string requestedPrefix, string requestedSuffix,
            bool matchCase, bool isSol, VanityTemplate[] templates, CancellationToken token, Action<long, ulong> progress)
        {
            token.ThrowIfCancellationRequested();
            int workers = Math.Max(1, Math.Min(128, Environment.ProcessorCount - 1));
            VanityGpuGenerator.Result result = null;
            var resultLock = new object();
            var watch = Stopwatch.StartNew();
            long attempts = 0, lastReport = 0;
            bool Matches(string address) => templates.Length != 0 ? templates.Any(template => template.Matches(address, isSol, matchCase))
                : isSol ? SolanaVanityGenerator.Matches(address, requestedPrefix, requestedSuffix, matchCase)
                : VanityGpuGenerator.Matches(address, requestedPrefix, requestedSuffix, matchCase);
            Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers }, (index, loop) => {
                while (!token.IsCancellationRequested && !loop.IsStopped)
                {
                    var solKey = isSol ? new Account() : null;
                    var evmKey = isSol ? null : EthECKey.GenerateKey();
                    string address = isSol ? solKey.PublicKey.Key : evmKey.GetPublicAddress();
                    long count = Interlocked.Increment(ref attempts);
                    if ((count & 15) == 0)
                    {
                        lock (resultLock)
                        {
                            long now = watch.ElapsedMilliseconds;
                            if (now - lastReport >= 200)
                            {
                                lastReport = now;
                                long total = Interlocked.Read(ref attempts);
                                progress(total, (ulong)(total * 1000.0 / now));
                            }
                        }
                    }
                    if (!Matches(address)) continue;
                    lock (resultLock)
                    {
                        if (result == null)
                        {
                            var candidate = new VanityGpuGenerator.Result { Address = address, PrivateKey = isSol ? solKey.PrivateKey.Key : evmKey.GetPrivateKey() };
                            if (isSol) SolanaVanityGenerator.VerifyResult(candidate);
                            result = candidate;
                        }
                    }
                    loop.Stop();
                }
            });
            progress(attempts, (ulong)(attempts / Math.Max(0.001, watch.Elapsed.TotalSeconds)));
            if (result == null) token.ThrowIfCancellationRequested();
            return result;
        }

        private string SaveVanityWallet(VanityGpuGenerator.Result result, string requestedPrefix, string requestedSuffix, bool matchCase, bool useGpu, bool isSol,
            VanityTemplate template = null, int stopMode = 0, int requestedCount = 1, string runId = null, long runIndex = 1, VanityTemplate[] searchTemplates = null)
        {
            Directory.CreateDirectory(vanityOutputDirectory);
            string path = Path.Combine(vanityOutputDirectory, (isSol ? "vanity-sol-" : "vanity-evm-") + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".json");
            var matches = template == null ? new VanityTemplate[0] : (searchTemplates ?? new[] { template })
                .Where(item => item.Matches(result.Address, isSol, matchCase)).OrderByDescending(item => item.Length).ToArray();
            if (template != null && matches.Length == 0) throw new InvalidOperationException("生成结果不符合所选模板。");
            var record = new JObject {
                ["createdAt"] = DateTimeOffset.Now.ToString("o"),
                ["network"] = isSol ? "SOL" : "EVM (ETH / BSC)",
                ["engine"] = useGpu ? "GPU" : "CPU",
                ["address"] = result.Address,
                ["privateKeyFormat"] = isSol ? "base58-64-byte-keypair" : "hex-secp256k1",
                ["prefix"] = requestedPrefix,
                ["suffix"] = requestedSuffix,
                ["caseSensitive"] = matchCase,
                ["searchMode"] = template == null ? "literal" : "template",
                ["template"] = matches.FirstOrDefault()?.Pattern,
                ["templatePosition"] = template == null ? null : template.AtEnd ? "suffix" : "prefix",
                ["stopPolicy"] = stopMode == 2 ? "continuous" : stopMode == 1 ? "count" : "first",
                ["targetCount"] = stopMode == 2 ? JValue.CreateNull() : new JValue(requestedCount),
                ["runId"] = runId,
                ["runIndex"] = runIndex
            };
            if (template != null)
            {
                record["primaryTemplate"] = template.Pattern;
                record["matchedTemplates"] = new JArray(matches.Select(item => item.Pattern));
                if (matches.Any(item => item.Kind == 1)) record["matchedRepeatLength"] = template.RepeatLength(result.Address, isSol, matchCase);
            }
            VanityWalletProtection.Protect(record, result.PrivateKey);
            VanityWalletProtection.Write(path, record);
            return path;
        }

        private void RefreshGeneratedWallet()
        {
            if (InvokeRequired) { Invoke(new Action(RefreshGeneratedWallet)); return; }
            string keyText = "[已隐藏，可勾选“显示本轮私钥”查看]";
            if (showPrivateKeys)
            {
                try { keyText = generatedWalletFile == null ? generatedPrivateKey : ReadVanityKey(generatedWalletFile, generatedAddress); }
                catch { keyText = "[无法解密，请检查 Windows 用户和钱包文件]"; }
            }
            labelResults.Text = "钱包链：" + generatedWalletNetwork + Environment.NewLine + "地址：" + generatedAddress + Environment.NewLine +
                "私钥：" + keyText +
                (generatedWalletFile == null ? "" : Environment.NewLine + "已加密保存：" + generatedWalletFile);
        }

        private void UpdateResultsLabel(string result)
        {
            // 确保在UI线程中更新控件
            if (labelResults.InvokeRequired)
            {
                labelResults.Invoke(new Action<string>(UpdateResultsLabel), result);
            }
            else
            {
                // 将新结果追加到现有内容中，并添加换行符
                labelResults.AppendText(Environment.NewLine + result + Environment.NewLine);
            }
        }


        private static string FormatVanityNumber(double value)
        {
            return value.ToString(Math.Abs(value) >= 1000000 ? "0.###E+0" : "N0",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FormatVanityTime(double seconds)
        {
            if (seconds < 1) return "< 1 秒";
            const double secondsPerYear = 365.25 * 86400;
            if (seconds >= secondsPerYear) return (seconds / secondsPerYear).ToString(seconds / secondsPerYear >= 1000000 ? "0.###E+0" : "0.##") + " 年";
            return EthAddressDifficultyCalculator.FormatTime(seconds);
        }

        private void UpdateGeneratedCountLabel()
        {
            if (generated_lab.InvokeRequired)
            {
                generated_lab.Invoke(new Action(UpdateGeneratedCountLabel));
                return;
            }
            generated_lab.Text = FormatVanityNumber(Interlocked.Read(ref totalGeneratedCount));
        }

        private void stop_btn_Click(object sender, EventArgs e)
        {
            if (!vanityRunning || cancellationTokenSource == null) return;
            cancellationTokenSource.Cancel();
            stop_btn.Enabled = false;
            labelResults.Text = "正在停止，请稍候…";
            ygtime.Text = "正在停止…";
        }
    }
}
