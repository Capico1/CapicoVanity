namespace jmsTools
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.BalanceInfo = new System.Windows.Forms.Label();
            this.text_transaction = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.oneaddress_cx = new System.Windows.Forms.Button();
            this.oneaddress = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.生成配置 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.stop_btn = new System.Windows.Forms.Button();
            this.textBoxPrefix = new System.Windows.Forms.TextBox();
            this.generate_btn = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.textBoxSuffix = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.xample_str = new System.Windows.Forms.RichTextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.labelResults = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.ygtime = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.probability_lab = new System.Windows.Forms.Label();
            this.labbel10 = new System.Windows.Forms.Label();
            this.generated_lab = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.complexity_lab = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.comboBoxMainNet = new System.Windows.Forms.ComboBox();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.tabControl1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.生成配置.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage2);

            this.tabControl1.Controls.Add(this.tabPage3);

            this.tabControl1.Location = new System.Drawing.Point(12, 44);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(916, 440);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.BalanceInfo);
            this.tabPage2.Controls.Add(this.text_transaction);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.oneaddress_cx);
            this.tabPage2.Controls.Add(this.oneaddress);
            this.tabPage2.Controls.Add(this.label2);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.tabPage2.Size = new System.Drawing.Size(908, 414);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "单地址查询";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // BalanceInfo
            // 
            this.BalanceInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.BalanceInfo.AutoSize = true;
            this.BalanceInfo.Location = new System.Drawing.Point(88, 55);
            this.BalanceInfo.Name = "BalanceInfo";
            this.BalanceInfo.Size = new System.Drawing.Size(11, 12);
            this.BalanceInfo.TabIndex = 6;
            this.BalanceInfo.Text = "0";
            // 
            // text_transaction
            // 
            this.text_transaction.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.text_transaction.Location = new System.Drawing.Point(17, 89);
            this.text_transaction.Multiline = true;
            this.text_transaction.Name = "text_transaction";
            this.text_transaction.Size = new System.Drawing.Size(867, 303);
            this.text_transaction.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(17, 55);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 12);
            this.label3.TabIndex = 3;
            this.label3.Text = "持仓分布：";
            // 
            // oneaddress_cx
            // 
            this.oneaddress_cx.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.oneaddress_cx.Location = new System.Drawing.Point(540, 16);
            this.oneaddress_cx.Name = "oneaddress_cx";
            this.oneaddress_cx.Size = new System.Drawing.Size(75, 23);
            this.oneaddress_cx.TabIndex = 2;
            this.oneaddress_cx.Text = "查询";
            this.oneaddress_cx.UseVisualStyleBackColor = true;
            this.oneaddress_cx.Click += new System.EventHandler(this.oneaddress_cx_Click);
            // 
            // oneaddress
            // 
            this.oneaddress.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.oneaddress.Location = new System.Drawing.Point(182, 16);
            this.oneaddress.Name = "oneaddress";
            this.oneaddress.Size = new System.Drawing.Size(339, 21);
            this.oneaddress.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(161, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "请输入对应主网的钱包地址：";
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.生成配置);
            this.tabPage3.Controls.Add(this.groupBox1);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(908, 414);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "靓号生成";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // 生成配置
            // 
            this.生成配置.Controls.Add(this.label4);
            this.生成配置.Controls.Add(this.stop_btn);
            this.生成配置.Controls.Add(this.textBoxPrefix);
            this.生成配置.Controls.Add(this.generate_btn);
            this.生成配置.Controls.Add(this.label5);
            this.生成配置.Controls.Add(this.label7);
            this.生成配置.Controls.Add(this.textBoxSuffix);
            this.生成配置.Controls.Add(this.label6);
            this.生成配置.Controls.Add(this.checkBox1);
            this.生成配置.Controls.Add(this.xample_str);
            this.生成配置.Location = new System.Drawing.Point(20, 18);
            this.生成配置.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.生成配置.Name = "生成配置";
            this.生成配置.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.生成配置.Size = new System.Drawing.Size(469, 316);
            this.生成配置.TabIndex = 12;
            this.生成配置.TabStop = false;
            this.生成配置.Text = "生成配置";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(41, 145);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(41, 12);
            this.label4.TabIndex = 0;
            this.label4.Text = "例如：";
            // 
            // stop_btn
            // 
            this.stop_btn.Location = new System.Drawing.Point(213, 215);
            this.stop_btn.Name = "stop_btn";
            this.stop_btn.Size = new System.Drawing.Size(75, 23);
            this.stop_btn.TabIndex = 10;
            this.stop_btn.Text = "停止";
            this.stop_btn.UseVisualStyleBackColor = true;
            this.stop_btn.Click += new System.EventHandler(this.stop_btn_Click);
            // 
            // textBoxPrefix
            // 
            this.textBoxPrefix.Location = new System.Drawing.Point(88, 28);
            this.textBoxPrefix.Name = "textBoxPrefix";
            this.textBoxPrefix.Size = new System.Drawing.Size(100, 21);
            this.textBoxPrefix.TabIndex = 1;
            this.textBoxPrefix.TextChanged += new System.EventHandler(this.textBoxPrefix_TextChanged);
            // 
            // generate_btn
            // 
            this.generate_btn.Location = new System.Drawing.Point(98, 215);
            this.generate_btn.Name = "generate_btn";
            this.generate_btn.Size = new System.Drawing.Size(75, 23);
            this.generate_btn.TabIndex = 9;
            this.generate_btn.Text = "生成";
            this.generate_btn.UseVisualStyleBackColor = true;
            this.generate_btn.Click += new System.EventHandler(this.generate_btn_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(41, 32);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(41, 12);
            this.label5.TabIndex = 2;
            this.label5.Text = "前缀：";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(41, 105);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(53, 12);
            this.label7.TabIndex = 8;
            this.label7.Text = "线程数：";
            // 
            // textBoxSuffix
            // 
            this.textBoxSuffix.Location = new System.Drawing.Point(269, 29);
            this.textBoxSuffix.Name = "textBoxSuffix";
            this.textBoxSuffix.Size = new System.Drawing.Size(100, 21);
            this.textBoxSuffix.TabIndex = 3;
            this.textBoxSuffix.TextChanged += new System.EventHandler(this.textBoxSuffix_TextChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(222, 33);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(41, 12);
            this.label6.TabIndex = 4;
            this.label6.Text = "后缀：";
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(43, 69);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(84, 16);
            this.checkBox1.TabIndex = 6;
            this.checkBox1.Text = "区分大小写";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // xample_str
            // 
            this.xample_str.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.xample_str.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.xample_str.Location = new System.Drawing.Point(88, 139);
            this.xample_str.Name = "xample_str";
            this.xample_str.ReadOnly = true;
            this.xample_str.Size = new System.Drawing.Size(326, 24);
            this.xample_str.TabIndex = 5;
            this.xample_str.Text = "0xa83114A443dA1CecEFC50368531cACE9F37fCCcb";
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.labelResults);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.ygtime);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.probability_lab);
            this.groupBox1.Controls.Add(this.labbel10);
            this.groupBox1.Controls.Add(this.generated_lab);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.complexity_lab);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Location = new System.Drawing.Point(516, 18);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(362, 316);
            this.groupBox1.TabIndex = 11;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "详情";
            // 
            // labelResults
            // 
            this.labelResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelResults.Location = new System.Drawing.Point(25, 171);
            this.labelResults.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.labelResults.Multiline = true;
            this.labelResults.Name = "labelResults";
            this.labelResults.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.labelResults.Size = new System.Drawing.Size(307, 135);
            this.labelResults.TabIndex = 9;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(23, 151);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(41, 12);
            this.label11.TabIndex = 8;
            this.label11.Text = "输出：";
            // 
            // ygtime
            // 
            this.ygtime.AutoSize = true;
            this.ygtime.Location = new System.Drawing.Point(82, 108);
            this.ygtime.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.ygtime.Name = "ygtime";
            this.ygtime.Size = new System.Drawing.Size(11, 12);
            this.ygtime.TabIndex = 7;
            this.ygtime.Text = "0";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(23, 108);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(65, 12);
            this.label9.TabIndex = 6;
            this.label9.Text = "预估时间：";
            // 
            // probability_lab
            // 
            this.probability_lab.AutoSize = true;
            this.probability_lab.Location = new System.Drawing.Point(82, 81);
            this.probability_lab.Name = "probability_lab";
            this.probability_lab.Size = new System.Drawing.Size(11, 12);
            this.probability_lab.TabIndex = 5;
            this.probability_lab.Text = "0";
            // 
            // labbel10
            // 
            this.labbel10.AutoSize = true;
            this.labbel10.Location = new System.Drawing.Point(23, 81);
            this.labbel10.Name = "labbel10";
            this.labbel10.Size = new System.Drawing.Size(59, 12);
            this.labbel10.TabIndex = 4;
            this.labbel10.Text = "50%概率：";
            // 
            // generated_lab
            // 
            this.generated_lab.AutoSize = true;
            this.generated_lab.Location = new System.Drawing.Point(82, 52);
            this.generated_lab.Name = "generated_lab";
            this.generated_lab.Size = new System.Drawing.Size(11, 12);
            this.generated_lab.TabIndex = 3;
            this.generated_lab.Text = "0";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(23, 52);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(53, 12);
            this.label10.TabIndex = 2;
            this.label10.Text = "已生成：";
            // 
            // complexity_lab
            // 
            this.complexity_lab.AutoSize = true;
            this.complexity_lab.Location = new System.Drawing.Point(82, 27);
            this.complexity_lab.Name = "complexity_lab";
            this.complexity_lab.Size = new System.Drawing.Size(11, 12);
            this.complexity_lab.TabIndex = 1;
            this.complexity_lab.Text = "0";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(23, 27);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(53, 12);
            this.label8.TabIndex = 0;
            this.label8.Text = "复杂度：";
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(20, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(77, 12);
            this.label1.TabIndex = 6;
            this.label1.Text = "请选择主网：";
            // 
            // comboBoxMainNet
            // 
            this.comboBoxMainNet.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxMainNet.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxMainNet.FormattingEnabled = true;
            this.comboBoxMainNet.Items.AddRange(new object[] {
            "ETH",
            "BSC",
            "SOL"});
            this.comboBoxMainNet.Location = new System.Drawing.Point(102, 18);
            this.comboBoxMainNet.Name = "comboBoxMainNet";
            this.comboBoxMainNet.Size = new System.Drawing.Size(121, 20);
            this.comboBoxMainNet.TabIndex = 5;
            this.comboBoxMainNet.SelectedIndexChanged += new System.EventHandler(this.comboBoxMainNet_SelectedIndexChanged);
            // 
            // linkLabel1
            // 
            this.linkLabel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(860, 22);
            this.linkLabel1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(65, 12);
            this.linkLabel1.TabIndex = 7;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "币圈工具箱";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(958, 510);

            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboBoxMainNet);
            this.Controls.Add(this.tabControl1);
            this.Name = "Form1";
            this.Text = "Nana 靓号";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.生成配置.ResumeLayout(false);
            this.生成配置.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBoxMainNet;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button oneaddress_cx;
        private System.Windows.Forms.TextBox oneaddress;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TextBox text_transaction;
        private System.Windows.Forms.Label BalanceInfo;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBoxPrefix;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBoxSuffix;
        private System.Windows.Forms.RichTextBox xample_str;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button generate_btn;
        private System.Windows.Forms.Button stop_btn;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label complexity_lab;
        private System.Windows.Forms.Label probability_lab;
        private System.Windows.Forms.Label labbel10;
        private System.Windows.Forms.Label generated_lab;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label ygtime;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox labelResults;
        private System.Windows.Forms.GroupBox 生成配置;
    }
}

