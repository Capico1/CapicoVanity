using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using jmsTools.Manage;
using Newtonsoft.Json.Linq;

namespace jmsTools
{
    public partial class Form1
    {
        private TabPage savedVanityTab;
        private ListView savedVanityList;
        private Label savedVanityStatus;
        private System.Windows.Forms.TextBox savedVanityAddress;
        private System.Windows.Forms.TextBox savedVanityPrivateKey;
        private Label savedVanityMetadata;
        private CheckBox revealSavedVanityKey;
        private System.Windows.Forms.Button copySavedVanityAddress;
        private System.Windows.Forms.Button copySavedVanityKey;
        private System.Windows.Forms.Button viewSavedVanities;
        private System.Windows.Forms.Button deleteSavedVanity;
        private Func<string, bool> confirmVanityDeletion;
        private readonly HashSet<string> savedVanityFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class SavedVanityWallet
        {
            public string Path, Network, Address, Engine, Prefix, Suffix, Template;
            public DateTimeOffset CreatedAt;
        }

        private void InitializeVanityWalletList()
        {
            savedVanityTab = new TabPage { Name = "savedVanityTab", Text = "已生成靓号", Padding = new Padding(8) };
            savedVanityList = new ListView {
                Name = "savedVanityList", Dock = DockStyle.Fill, View = View.Details,
                FullRowSelect = true, HideSelection = false, MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            savedVanityList.Columns.Add("生成时间", 145);
            savedVanityList.Columns.Add("钱包链", 108);
            savedVanityList.Columns.Add("地址", 420);
            savedVanityList.Columns.Add("引擎", 60);
            savedVanityList.Resize += (sender, args) => {
                savedVanityList.Columns[2].Width = Math.Max(160, savedVanityList.ClientSize.Width - 335);
            };
            savedVanityList.SelectedIndexChanged += (sender, args) => {
                revealSavedVanityKey.Checked = false;
                RefreshSavedVanityWalletDetails();
            };
            savedVanityTab.Leave += (sender, args) => revealSavedVanityKey.Checked = false;
            savedVanityList.KeyDown += (sender, args) => {
                if (args.Control && args.KeyCode == Keys.C) { CopySavedVanityValue(false); args.SuppressKeyPress = true; }
            };

            var toolbar = new FlowLayoutPanel {
                Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
                Padding = new Padding(0, 0, 0, 5)
            };
            var refresh = new System.Windows.Forms.Button { Text = "刷新列表", AutoSize = true };
            refresh.Click += (sender, args) => LoadSavedVanityWallets();
            var folder = new System.Windows.Forms.Button { Text = "打开保存文件夹", AutoSize = true };
            folder.Click += (sender, args) => {
                try {
                    Directory.CreateDirectory(vanityOutputDirectory);
                    Process.Start(new ProcessStartInfo(vanityOutputDirectory) { UseShellExecute = true });
                }
                catch (Exception ex) { savedVanityStatus.Text = "打开文件夹失败：" + ex.Message; }
            };
            revealSavedVanityKey = new CheckBox { Text = "显示选中钱包的私钥", AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
            revealSavedVanityKey.CheckedChanged += (sender, args) => RefreshSavedVanityWalletDetails();
            toolbar.Controls.Add(refresh);
            toolbar.Controls.Add(folder);
            deleteSavedVanity = new System.Windows.Forms.Button { Text = "删除选中", AutoSize = true, Enabled = false };
            confirmVanityDeletion = address => MessageBox.Show(this,
                "确定删除选中的靓号钱包吗？\n\n地址：" + address +
                "\n\n钱包文件将移至保存目录下的 Deleted 文件夹，可手动移回恢复。", "删除靓号钱包",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            deleteSavedVanity.Click += (sender, args) => DeleteSelectedVanityWallet();
            toolbar.Controls.Add(deleteSavedVanity);
            toolbar.Controls.Add(revealSavedVanityKey);

            var details = new TableLayoutPanel {
                Dock = DockStyle.Bottom, Height = 104, ColumnCount = 3, RowCount = 3,
                Padding = new Padding(0, 8, 0, 0)
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            savedVanityAddress = new System.Windows.Forms.TextBox { Name = "savedVanityAddress", ReadOnly = true, Dock = DockStyle.Fill };
            savedVanityPrivateKey = new System.Windows.Forms.TextBox {
                Name = "savedVanityPrivateKey", ReadOnly = true, Dock = DockStyle.Fill, UseSystemPasswordChar = true
            };
            copySavedVanityAddress = new System.Windows.Forms.Button { Text = "复制地址", Dock = DockStyle.Fill };
            copySavedVanityKey = new System.Windows.Forms.Button { Text = "复制私钥", Dock = DockStyle.Fill };
            copySavedVanityAddress.Click += (sender, args) => CopySavedVanityValue(false);
            copySavedVanityKey.Click += (sender, args) => CopySavedVanityValue(true);
            details.Controls.Add(new Label { Text = "地址：", AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 0);
            details.Controls.Add(savedVanityAddress, 1, 0);
            details.Controls.Add(copySavedVanityAddress, 2, 0);
            details.Controls.Add(new Label { Text = "私钥：", AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 1);
            details.Controls.Add(savedVanityPrivateKey, 1, 1);
            details.Controls.Add(copySavedVanityKey, 2, 1);
            savedVanityMetadata = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.DimGray };
            details.Controls.Add(savedVanityMetadata, 0, 2);
            details.SetColumnSpan(savedVanityMetadata, 3);
            savedVanityStatus = new Label { Dock = DockStyle.Bottom, Height = 30, ForeColor = Color.DimGray, AutoEllipsis = true };
            savedVanityTab.Controls.Add(savedVanityList);
            savedVanityTab.Controls.Add(details);
            savedVanityTab.Controls.Add(savedVanityStatus);
            savedVanityTab.Controls.Add(toolbar);
            tabControl1.TabPages.Add(savedVanityTab);

            viewSavedVanities = new System.Windows.Forms.Button {
                Text = "查看已生成靓号", Location = new Point(98, vanityBatch == null ? 282 : 316), Size = new Size(190, 25)
            };
            viewSavedVanities.Click += (sender, args) => tabControl1.SelectedTab = savedVanityTab;
            生成配置.Controls.Add(viewSavedVanities);
            LoadSavedVanityWallets();
        }

        private SavedVanityWallet ReadSavedVanityWallet(string path)
        {
            var record = VanityWalletProtection.Read(path);
            string network = (string)record["network"];
            string address = (string)record["address"];
            DateTimeOffset created;
            if ((network != "SOL" && network != "EVM (ETH / BSC)") || string.IsNullOrWhiteSpace(address) ||
                !VanityWalletProtection.IsProtected(record) || address.Length > 128 ||
                !DateTimeOffset.TryParse((string)record["createdAt"], CultureInfo.InvariantCulture, DateTimeStyles.None, out created))
                throw new InvalidDataException("钱包文件格式无效。");
            return new SavedVanityWallet {
                Path = System.IO.Path.GetFullPath(path), CreatedAt = created, Network = network == "SOL" ? "SOL" : "ETH / BSC",
                Address = address, Engine = (string)record["engine"] ?? "—",
                Prefix = (string)record["prefix"] ?? "", Suffix = (string)record["suffix"] ?? "",
                Template = (string)record["searchMode"] == "template" ? ((string)record["template"] ?? "") +
                    ((string)record["templatePosition"] == "prefix" ? "（头部）" : "（尾部）") +
                    ((int?)record["matchedRepeatLength"] is int repeat ? " · 实际 " + repeat + " 位相同" : "") : null
            };
        }

        private ListViewItem SavedVanityRow(SavedVanityWallet wallet)
        {
            var row = new ListViewItem(wallet.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")) { Tag = wallet };
            row.SubItems.Add(wallet.Network);
            row.SubItems.Add(wallet.Address);
            row.SubItems.Add(wallet.Engine);
            return row;
        }

        private void LoadSavedVanityWallets()
        {
            string selected = SelectedSavedVanityWallet() == null ? null : SelectedSavedVanityWallet().Path;
            var wallets = new List<SavedVanityWallet>();
            int skipped = 0, migrated, failed;
            try
            {
                VanityWalletProtection.Migrate(vanityOutputDirectory, out migrated, out failed);
                if (Directory.Exists(vanityOutputDirectory))
                    foreach (string path in Directory.EnumerateFiles(vanityOutputDirectory, "vanity-*.json")
                        .OrderByDescending(File.GetLastWriteTimeUtc).Take(5000))
                    {
                        try { wallets.Add(ReadSavedVanityWallet(path)); }
                        catch { skipped++; }
                    }
            }
            catch (Exception ex) { savedVanityStatus.Text = "读取保存文件夹失败：" + ex.Message; return; }
            savedVanityList.BeginUpdate();
            try
            {
                savedVanityList.Items.Clear(); savedVanityFiles.Clear();
                foreach (var wallet in wallets.OrderByDescending(value => value.CreatedAt))
                {
                    if (!savedVanityFiles.Add(wallet.Path)) continue;
                    var row = SavedVanityRow(wallet);
                    savedVanityList.Items.Add(row);
                    if (string.Equals(wallet.Path, selected, StringComparison.OrdinalIgnoreCase)) row.Selected = true;
                }
                if (savedVanityList.SelectedItems.Count == 0 && savedVanityList.Items.Count > 0) savedVanityList.Items[0].Selected = true;
            }
            finally { savedVanityList.EndUpdate(); }
            UpdateSavedVanityCount();
            savedVanityStatus.Text = (wallets.Count == 0 ? "尚无已保存的靓号，生成成功后会自动加入此列表。" : "已从本地文件恢复列表。") +
                (migrated == 0 ? "" : " 已加密旧钱包 " + migrated + " 个。") +
                (failed == 0 ? "" : " " + failed + " 个文件未能加密，原文件保留。") +
                (skipped == 0 ? "" : " 已跳过 " + skipped + " 个不可用文件。") + " 私钥按需解密。";
            RefreshSavedVanityWalletDetails();
        }

        private void AddSavedVanityWallet(string path)
        {
            if (savedVanityList == null) return;
            try
            {
                var wallet = ReadSavedVanityWallet(path);
                if (!savedVanityFiles.Add(wallet.Path)) return;
                var row = SavedVanityRow(wallet);
                savedVanityList.Items.Insert(0, row);
                while (savedVanityList.Items.Count > 5000)
                {
                    var oldest = savedVanityList.Items[savedVanityList.Items.Count - 1];
                    savedVanityFiles.Remove(((SavedVanityWallet)oldest.Tag).Path);
                    savedVanityList.Items.Remove(oldest);
                }
                foreach (ListViewItem selected in savedVanityList.SelectedItems.Cast<ListViewItem>().ToArray()) selected.Selected = false;
                row.Selected = true; row.Focused = true;
                UpdateSavedVanityCount();
                RefreshSavedVanityWalletDetails();
                savedVanityStatus.Text = "已加密保存。仅在当前 Windows 用户下查看或复制私钥。";
            }
            catch (Exception ex) { savedVanityStatus.Text = "钱包文件已保存，列表更新失败，可点击刷新：" + ex.Message; }
        }

        private void UpdateSavedVanityCount()
        {
            savedVanityTab.Text = "已生成靓号 (" + savedVanityList.Items.Count + ")";
            viewSavedVanities.Text = "查看已生成靓号 (" + savedVanityList.Items.Count + ")";
            if (savedVanityList.Items.Count >= 5000) savedVanityTab.Text = "已生成靓号（最近 5000）";
        }

        private SavedVanityWallet SelectedSavedVanityWallet()
        {
            return savedVanityList == null || savedVanityList.SelectedItems.Count == 0 ? null : savedVanityList.SelectedItems[0].Tag as SavedVanityWallet;
        }

        private void RefreshSavedVanityWalletDetails()
        {
            if (savedVanityPrivateKey == null) return;
            var wallet = SelectedSavedVanityWallet();
            savedVanityAddress.Text = wallet == null ? "" : wallet.Address;
            savedVanityPrivateKey.UseSystemPasswordChar = !revealSavedVanityKey.Checked;
            // Keep hidden secrets out of the text control until explicitly revealed.
            savedVanityPrivateKey.Text = wallet == null ? "" : "已隐藏";
            if (wallet != null && revealSavedVanityKey.Checked)
            {
                try { savedVanityPrivateKey.Text = ReadVanityKey(wallet.Path, wallet.Address); }
                catch { savedVanityStatus.Text = "无法解密私钥，请使用原 Windows 用户并确认钱包文件完好。"; }
            }
            savedVanityMetadata.Text = wallet == null ? "选中一条记录查看详情。" : wallet.Template != null
                ? wallet.Network + " · 模板：" + wallet.Template
                : wallet.Network + " · 前缀：" + (wallet.Prefix.Length == 0 ? "无" : wallet.Prefix) + " · 后缀：" + (wallet.Suffix.Length == 0 ? "无" : wallet.Suffix);
            copySavedVanityAddress.Enabled = copySavedVanityKey.Enabled = wallet != null;
            deleteSavedVanity.Enabled = wallet != null;
        }

        private static string ReadVanityKey(string path, string address)
        {
            var record = VanityWalletProtection.Read(path);
            if (!string.Equals((string)record["address"], address, StringComparison.Ordinal))
                throw new InvalidDataException("钱包地址已更改。");
            return VanityWalletProtection.Decrypt(record);
        }

        private void DeleteSelectedVanityWallet()
        {
            var wallet = SelectedSavedVanityWallet();
            if (wallet == null || !confirmVanityDeletion(wallet.Address)) return;
            try
            {
                string deletedDirectory = Path.Combine(vanityOutputDirectory, "Deleted");
                Directory.CreateDirectory(deletedDirectory);
                string destination = Path.Combine(deletedDirectory, Path.GetFileName(wallet.Path));
                if (File.Exists(destination))
                    destination = Path.Combine(deletedDirectory, Path.GetFileNameWithoutExtension(wallet.Path) + "-" + Guid.NewGuid().ToString("N") + ".json");
                File.Move(wallet.Path, destination);
                revealSavedVanityKey.Checked = false;
                LoadSavedVanityWallets();
                savedVanityStatus.Text = "已删除选中记录；钱包文件保留在 Deleted 文件夹，移回保存目录后刷新可恢复。";
            }
            catch (Exception ex) { savedVanityStatus.Text = "删除失败，记录已保留：" + ex.Message; }
        }

        private void CopySavedVanityValue(bool privateKey)
        {
            var wallet = SelectedSavedVanityWallet();
            if (wallet == null) return;
            try {
                if (privateKey)
                {
                    if (!ConfirmAndCopyWalletSecret("私钥", () => ReadVanityKey(wallet.Path, wallet.Address))) return;
                }
                else Clipboard.SetText(wallet.Address);
                savedVanityStatus.Text = privateKey ? "已复制选中钱包的私钥。" : "已复制选中钱包的地址。";
            }
            catch { savedVanityStatus.Text = "复制失败，请检查剪贴板权限和钱包文件。"; }
        }
    }
}
