using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using jmsTools.Manage;

namespace jmsTools
{
    internal sealed class GpuConfigurationControl : UserControl
    {
        private readonly ListView devices = new ListView();
        private readonly ComboBox batch = new ComboBox();
        private readonly CheckBox enabled = new CheckBox();
        private readonly Label status = new Label();
        private readonly Button refresh = new Button();
        private readonly Button save = new Button();
        private readonly Button test = new Button();
        private readonly Button cancel = new Button();
        private GpuComputeSettings settings = new GpuComputeSettings();
        private CancellationTokenSource testCancellation;
        private bool initialized, busy;
        private string loadError;

        public GpuConfigurationControl()
        {
            Name = "gpuConfiguration";
            Padding = new Padding(10);
            if (File.Exists(GpuComputeSettings.ConfigurationPath))
            {
                try { settings = GpuComputeSettings.Load(); }
                catch (Exception) { loadError = "已保存的 GPU 配置无法读取，请重新选择并保存。"; }
            }
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            refresh.Name = "refreshGpuDevices"; refresh.Text = "检测 GPU"; refresh.AutoSize = true;
            save.Name = "saveGpuConfiguration"; save.Text = "保存配置"; save.AutoSize = true;
            test.Name = "testGpuCompute"; test.Text = "GPU 计算自检"; test.AutoSize = true;
            cancel.Name = "cancelGpuCompute"; cancel.Text = "取消自检"; cancel.AutoSize = true; cancel.Enabled = false;
            var example = new Button { Text = "复制对接示例", AutoSize = true };
            commands.Controls.AddRange(new Control[] { refresh, save, test, cancel, example });
            layout.Controls.Add(commands, 0, 0);
            devices.Name = "gpuDeviceList";
            devices.View = View.Details; devices.FullRowSelect = true; devices.MultiSelect = false;
            devices.HideSelection = false; devices.Dock = DockStyle.Fill;
            devices.Columns.Add("GPU 设备（OpenCL）", 300);
            devices.Columns.Add("设备内存 GiB", 110);
            devices.Columns.Add("计算单元", 80);
            devices.Columns.Add("驱动版本", 130);
            devices.Columns.Add("状态", 75);
            devices.Resize += (sender, args) => devices.Columns[0].Width = Math.Max(200, devices.ClientSize.Width - 410);
            layout.Controls.Add(devices, 0, 1);
            var parameters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 8, 0, 5) };
            enabled.Name = "enableGenericGpu"; enabled.Text = "启用 GPU 任务"; enabled.AutoSize = true; enabled.Checked = settings.Enabled;
            batch.Name = "gpuBatchSize"; batch.DropDownStyle = ComboBoxStyle.DropDown; batch.Width = 140;
            batch.AccessibleName = "每批元素数";
            batch.Items.AddRange(new object[] { "16,384", "65,536", "262,144", "1,048,576", "4,194,304", "8,388,608", "16,777,216" });
            batch.Text = settings.BatchSize.ToString("N0", CultureInfo.InvariantCulture);
            parameters.Controls.Add(enabled);
            parameters.Controls.Add(new Label { Text = "每批元素数：", AutoSize = true, Margin = new Padding(15, 5, 3, 3) });
            parameters.Controls.Add(batch);
            parameters.Controls.Add(new Label { Text = "默认 1,048,576；最高 16,777,216，按任务测速选择", AutoSize = true, Margin = new Padding(10, 5, 3, 3) });
            layout.Controls.Add(parameters, 0, 2);
            status.Name = "gpuConfigurationStatus"; status.Text = "选择设备后保存；可先运行自检验证计算结果。";
            status.AutoSize = true; status.Dock = DockStyle.Fill; status.Padding = new Padding(0, 5, 0, 5);
            layout.Controls.Add(status, 0, 3);
            var help = new Label { AutoSize = true, Dock = DockStyle.Fill,
                Text = "此页供通用计算模块对接。目前提供数组加法接口；其他任务需接入相应算法。批次越大不一定越快。\n自检仅核对 65,539 个元素，不测试所选批次的最大容量；不保存计算过程。取消在当前批次结束后生效。" };
            layout.Controls.Add(help, 0, 4);
            Controls.Add(layout);
            refresh.Click += async (sender, args) => await RefreshDevicesAsync();
            save.Click += (sender, args) => SaveConfiguration();
            test.Click += async (sender, args) => await TestSelectedDeviceAsync();
            cancel.Click += (sender, args) => testCancellation?.Cancel();
            example.Click += (sender, args) => {
                try {
                    Clipboard.SetText("using jmsTools.Manage;\r\nusing System.Threading;\r\n\r\nvar config = GpuComputeSettings.Load();\r\nfloat[] result = await GpuComputeService.AddVectorsAsync(\r\n    new float[] { 1, 2, 3 }, new float[] { 4, 5, 6 },\r\n    config, CancellationToken.None);\r\n// result: 5, 7, 9\r\n");
                    status.Text = "C# 对接示例已复制。";
                }
                catch (Exception) { status.Text = "剪贴板暂不可用，请重试。"; }
            };
            devices.SelectedIndexChanged += (sender, args) => { if (!busy && initialized) status.Text = "设备选择已修改，点击“保存配置”后供其他模块读取。"; };
            enabled.CheckedChanged += (sender, args) => { if (initialized && !busy) status.Text = "设置已修改，尚未保存。"; };
            batch.TextChanged += (sender, args) => { if (initialized && !busy) status.Text = "批次大小已修改，尚未保存。"; };
        }

        protected override async void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !initialized && !busy) await RefreshDevicesAsync();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            refresh.Enabled = save.Enabled = test.Enabled = devices.Enabled = batch.Enabled = enabled.Enabled = !value;
            cancel.Enabled = value && testCancellation != null;
        }

        private GpuComputeSettings CurrentSettings()
        {
            var device = devices.SelectedItems.Count == 0 ? null : (GpuComputeDevice)devices.SelectedItems[0].Tag;
            if (enabled.Checked && device != null && !device.Available) throw new ArgumentException("该设备当前不可用，请选择其他 GPU。");
            int batchSize;
            var batchText = batch.Text.Trim();
            if (!Regex.IsMatch(batchText, @"^(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)$") ||
                !int.TryParse(batchText.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out batchSize))
                throw new ArgumentException("请选择批次档位，或输入 256 至 16,777,216 之间的整数。");
            var value = new GpuComputeSettings { Enabled = enabled.Checked, DeviceKey = device?.Key ?? "", BatchSize = batchSize };
            value.Validate();
            return value;
        }

        public async Task RefreshDevicesAsync()
        {
            if (busy || IsDisposed) return;
            string selected = devices.SelectedItems.Count > 0 ? ((GpuComputeDevice)devices.SelectedItems[0].Tag).Key : settings.DeviceKey;
            SetBusy(true);
            status.Text = "正在检测 GPU…";
            try
            {
                var found = await Task.Run(() => GpuComputeService.EnumerateDevices());
                if (IsDisposed) return;
                devices.Items.Clear();
                var preferred = found.FirstOrDefault(d => d.Available && d.Vendor.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) ?? found.FirstOrDefault(d => d.Available);
                if (string.IsNullOrEmpty(selected)) selected = preferred?.Key;
                foreach (var device in found)
                {
                    var item = new ListViewItem(new[] { device.Name, (device.MemoryBytes / 1073741824d).ToString("0.00"),
                        device.ComputeUnits.ToString(), device.Driver, device.Available ? "可用" : "不可用" }) { Tag = device };
                    devices.Items.Add(item);
                    if (device.Key == selected) item.Selected = true;
                }
                bool missing = !string.IsNullOrEmpty(selected) && !found.Any(d => d.Key == selected);
                status.Text = loadError ?? (found.Length == 0 ? "未检测到 OpenCL GPU，请检查显卡驱动。" : missing
                    ? "原先选择的设备已不可用，请重新选择后保存。"
                    : "已检测到 " + found.Length + " 个 GPU。选择设备并保存，或运行计算自检。");
                loadError = null;
            }
            catch (DllNotFoundException) { if (!IsDisposed) status.Text = "未安装 OpenCL 运行库，请安装显卡厂商驱动后重新检测。"; }
            catch (Exception) { if (!IsDisposed) status.Text = "GPU 检测失败，请检查显卡驱动后重试。"; }
            finally { if (!IsDisposed) { initialized = true; SetBusy(false); } }
        }

        private void SaveConfiguration()
        {
            try
            {
                var value = CurrentSettings();
                value.Save(); settings = value;
                status.Text = value.Enabled ? "GPU 配置已保存，生成与恢复任务将使用该设置。" : "已保存：GPU 任务已禁用。";
            }
            catch (ArgumentException ex) { status.Text = ex.Message; }
            catch (Exception) { status.Text = "保存失败，请检查程序目录写入权限。"; }
        }

        public async Task TestSelectedDeviceAsync()
        {
            if (busy || IsDisposed) return;
            try
            {
                var value = CurrentSettings();
                testCancellation = new CancellationTokenSource();
                SetBusy(true);
                status.Text = "正在 GPU 上计算并逐项核对结果…";
                var milliseconds = await GpuComputeService.SelfTestAsync(value, testCancellation.Token);
                if (!IsDisposed) status.Text = "GPU 自检通过：65,539 个数组元素全部正确，用时 " + milliseconds.ToString("0.0") + " ms（含初始化与传输）；配置需单独保存。";
            }
            catch (OperationCanceledException) { if (!IsDisposed) status.Text = "GPU 自检已取消。"; }
            catch (ArgumentException ex) { if (!IsDisposed) status.Text = ex.Message; }
            catch (InvalidOperationException ex) { if (!IsDisposed) status.Text = ex.Message; }
            catch (Exception) { if (!IsDisposed) status.Text = "GPU 自检失败，请检查设备和驱动。"; }
            finally
            {
                testCancellation?.Dispose(); testCancellation = null;
                if (!IsDisposed) SetBusy(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) testCancellation?.Cancel();
            base.Dispose(disposing);
        }
    }
}
