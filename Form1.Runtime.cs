using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using jmsTools.Manage;

namespace jmsTools
{
    public partial class Form1
    {
        private const int TransientLogLimit = 32768;
        private System.Windows.Forms.Timer transientLogTimer;
        private CancellationTokenSource publicQueryCancellation;
        private System.Windows.Forms.Button cancelPublicQuery;
        private readonly Stopwatch publicQueryWatch = new Stopwatch();
        private int publicSucceeded, publicFailed, publicSkipped, publicTimedOut;
        private string publicQueryState = "等待查询";
        private string publicQueryPending;
        private readonly List<PublicDisplayRecord> publicDisplayRecords = new List<PublicDisplayRecord>();
        private Func<string, string, CancellationToken, Task<BalanceQueryResult>> publicBalanceQuery =
            (address, network, token) => BlockManage.GetPublicAddressBalanceResultAsync(address, network, token);
        private sealed class PublicDisplayRecord
        {
            public string Text;
            public DateTime Expires;
        }

        private void InitializeTransientRecords()
        {
            cancelPublicQuery = new System.Windows.Forms.Button {
                Text = "取消查询", Location = new Point(oneaddress_cx.Right + 8, 14), AutoSize = true, Enabled = false
            };
            cancelPublicQuery.Click += (sender, args) => {
                cancelPublicQuery.Enabled = false;
                if (publicQueryCancellation != null) publicQueryCancellation.Cancel();
            };
            tabPage2.Controls.Add(cancelPublicQuery);
            BalanceInfo.AutoSize = false;
            BalanceInfo.Size = new Size(790, 32);
            BalanceInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            text_transaction.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabPage2.Resize += (sender, args) => LayoutPublicQueryResults();
            LayoutPublicQueryResults();
            transientLogTimer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
            transientLogTimer.Tick += (sender, args) => {
                CleanTransientRecords(DateTime.UtcNow);
                if (publicQueryCancellation != null) UpdatePublicQuerySummary();
            };
            transientLogTimer.Start();
            Disposed += (sender, args) => {
                if (publicQueryCancellation != null) publicQueryCancellation.Cancel();
                publicDisplayRecords.Clear();
            };
        }

        private void LayoutPublicQueryResults()
        {
            text_transaction.SetBounds(17, 178, Math.Max(120, tabPage2.ClientSize.Width - 34),
                Math.Max(80, tabPage2.ClientSize.Height - 193));
        }

        private void CleanTransientRecords(DateTime now)
        {
            if (publicDisplayRecords.RemoveAll(record => record.Expires <= now) > 0)
                RenderPublicQueryRecords();
        }

        private void AddPublicQueryRecord(string text, bool temporary)
        {
            publicQueryPending = null;
            if (text.Length > 16000) text = text.Substring(0, 16000) + "\r\n（显示已截断）";
            publicDisplayRecords.Add(new PublicDisplayRecord {
                Text = text, Expires = temporary ? DateTime.UtcNow.AddSeconds(60) : DateTime.MaxValue
            });
            while (publicDisplayRecords.Sum(record => record.Text.Length + 4) > TransientLogLimit)
                publicDisplayRecords.RemoveAt(0);
            RenderPublicQueryRecords();
        }

        private void RenderPublicQueryRecords(string pending = null)
        {
            if (pending != null) publicQueryPending = pending;
            var text = string.Join("\r\n\r\n", publicDisplayRecords.Select(record => record.Text));
            if (!string.IsNullOrEmpty(publicQueryPending)) text += "\r\n\r\n" + publicQueryPending;
            text_transaction.Text = text.Length <= TransientLogLimit ? text : text.Substring(text.Length - TransientLogLimit);
        }

        private void RecordPublicQueryOutcome(bool success, string error)
        {
            if (success) publicSucceeded++;
            else {
                publicFailed++;
                if ((error ?? "").Contains("超时")) publicTimedOut++;
            }
            UpdatePublicQuerySummary();
        }

        private void UpdatePublicQuerySummary()
        {
            double seconds = publicQueryWatch.Elapsed.TotalSeconds;
            BalanceInfo.Text = publicQueryState + " | 成功：" + publicSucceeded + " | 失败：" + publicFailed +
                " | 超时：" + publicTimedOut + " | 跳过：" + publicSkipped + " | 用时：" + seconds.ToString("0.0") + " 秒\r\n" +
                "成功/秒：" + (publicSucceeded / Math.Max(0.001, seconds)).ToString("0.00") +
                "；请求耗时含排队、限流和网络等待。失败详情 60 秒清理，不保存查询记录。";
        }
    }
}
