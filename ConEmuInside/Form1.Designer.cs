namespace ConEmuInside
{
    partial class ChildTerminal
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.startPanel = new System.Windows.Forms.Panel();
            this.argLog = new System.Windows.Forms.CheckBox();
            this.argDebug = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.xmlBtn = new System.Windows.Forms.Button();
            this.argXmlFile = new System.Windows.Forms.TextBox();
            this.startBtn = new System.Windows.Forms.Button();
            this.dirBtn = new System.Windows.Forms.Button();
            this.cmdBtn = new System.Windows.Forms.Button();
            this.exeBtn = new System.Windows.Forms.Button();
            this.argRunAs = new System.Windows.Forms.CheckBox();
            this.argDirectory = new System.Windows.Forms.TextBox();
            this.argCmdLine = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.argConEmuExe = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.termPanel = new System.Windows.Forms.Panel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.macroBtn = new System.Windows.Forms.Button();
            this.printBtn = new System.Windows.Forms.Button();
            this.promptBox = new System.Windows.Forms.TextBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.closeBtn = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.startPanel.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.startPanel);
            this.groupBox1.Controls.Add(this.termPanel);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(576, 355);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Terminal";
            // 
            // startPanel
            // 
            this.startPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.startPanel.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.startPanel.Controls.Add(this.argLog);
            this.startPanel.Controls.Add(this.argDebug);
            this.startPanel.Controls.Add(this.label4);
            this.startPanel.Controls.Add(this.xmlBtn);
            this.startPanel.Controls.Add(this.argXmlFile);
            this.startPanel.Controls.Add(this.startBtn);
            this.startPanel.Controls.Add(this.dirBtn);
            this.startPanel.Controls.Add(this.cmdBtn);
            this.startPanel.Controls.Add(this.exeBtn);
            this.startPanel.Controls.Add(this.argRunAs);
            this.startPanel.Controls.Add(this.argDirectory);
            this.startPanel.Controls.Add(this.argCmdLine);
            this.startPanel.Controls.Add(this.label3);
            this.startPanel.Controls.Add(this.label2);
            this.startPanel.Controls.Add(this.argConEmuExe);
            this.startPanel.Controls.Add(this.label1);
            this.startPanel.Location = new System.Drawing.Point(9, 17);
            this.startPanel.Name = "startPanel";
            this.startPanel.Size = new System.Drawing.Size(558, 191);
            this.startPanel.TabIndex = 0;
            this.startPanel.Text = "Start parameters";
            // 
            // argLog
            // 
            this.argLog.AutoSize = true;
            this.argLog.Location = new System.Drawing.Point(383, 128);
            this.argLog.Name = "argLog";
            this.argLog.Size = new System.Drawing.Size(108, 17);
            this.argLog.TabIndex = 16;
            this.argLog.Text = "ConEmu LogFiles";
            this.argLog.UseVisualStyleBackColor = true;
            // 
            // argDebug
            // 
            this.argDebug.AutoSize = true;
            this.argDebug.Location = new System.Drawing.Point(256, 128);
            this.argDebug.Name = "argDebug";
            this.argDebug.Size = new System.Drawing.Size(111, 17);
            this.argDebug.TabIndex = 15;
            this.argDebug.Text = "Wait for debugger";
            this.argDebug.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 101);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(68, 13);
            this.label4.TabIndex = 14;
            this.label4.Text = "ConEmu xml:";
            // 
            // xmlBtn
            // 
            this.xmlBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.xmlBtn.Location = new System.Drawing.Point(525, 97);
            this.xmlBtn.Name = "xmlBtn";
            this.xmlBtn.Size = new System.Drawing.Size(24, 21);
            this.xmlBtn.TabIndex = 13;
            this.xmlBtn.Text = "...";
            this.xmlBtn.UseVisualStyleBackColor = true;
            // 
            // argXmlFile
            // 
            this.argXmlFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.argXmlFile.Location = new System.Drawing.Point(117, 97);
            this.argXmlFile.Name = "argXmlFile";
            this.argXmlFile.Size = new System.Drawing.Size(402, 20);
            this.argXmlFile.TabIndex = 12;
            this.argXmlFile.Enter += new System.EventHandler(this.startArgs_Enter);
            // 
            // startBtn
            // 
            this.startBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.startBtn.Location = new System.Drawing.Point(117, 151);
            this.startBtn.Name = "startBtn";
            this.startBtn.Size = new System.Drawing.Size(169, 23);
            this.startBtn.TabIndex = 11;
            this.startBtn.Text = "&Start ConEmu";
            this.startBtn.UseVisualStyleBackColor = true;
            this.startBtn.Click += new System.EventHandler(this.startBtn_Click);
            // 
            // dirBtn
            // 
            this.dirBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dirBtn.Location = new System.Drawing.Point(525, 70);
            this.dirBtn.Name = "dirBtn";
            this.dirBtn.Size = new System.Drawing.Size(24, 21);
            this.dirBtn.TabIndex = 10;
            this.dirBtn.Text = "...";
            this.dirBtn.UseVisualStyleBackColor = true;
            this.dirBtn.Click += new System.EventHandler(this.dirBtn_Click);
            // 
            // cmdBtn
            // 
            this.cmdBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdBtn.Location = new System.Drawing.Point(525, 45);
            this.cmdBtn.Name = "cmdBtn";
            this.cmdBtn.Size = new System.Drawing.Size(24, 21);
            this.cmdBtn.TabIndex = 9;
            this.cmdBtn.Text = "...";
            this.cmdBtn.UseVisualStyleBackColor = true;
            this.cmdBtn.Click += new System.EventHandler(this.cmdBtn_Click);
            // 
            // exeBtn
            // 
            this.exeBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exeBtn.Location = new System.Drawing.Point(525, 18);
            this.exeBtn.Name = "exeBtn";
            this.exeBtn.Size = new System.Drawing.Size(24, 21);
            this.exeBtn.TabIndex = 8;
            this.exeBtn.Text = "...";
            this.exeBtn.UseVisualStyleBackColor = true;
            this.exeBtn.Click += new System.EventHandler(this.exeBtn_Click);
            // 
            // argRunAs
            // 
            this.argRunAs.AutoSize = true;
            this.argRunAs.Location = new System.Drawing.Point(117, 128);
            this.argRunAs.Name = "argRunAs";
            this.argRunAs.Size = new System.Drawing.Size(123, 17);
            this.argRunAs.TabIndex = 7;
            this.argRunAs.Text = "&Run as Administrator";
            this.argRunAs.UseVisualStyleBackColor = true;
            // 
            // argDirectory
            // 
            this.argDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.argDirectory.Location = new System.Drawing.Point(117, 71);
            this.argDirectory.Name = "argDirectory";
            this.argDirectory.Size = new System.Drawing.Size(402, 20);
            this.argDirectory.TabIndex = 6;
            this.argDirectory.Enter += new System.EventHandler(this.startArgs_Enter);
            // 
            // argCmdLine
            // 
            this.argCmdLine.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.argCmdLine.Location = new System.Drawing.Point(117, 45);
            this.argCmdLine.Name = "argCmdLine";
            this.argCmdLine.Size = new System.Drawing.Size(402, 20);
            this.argCmdLine.TabIndex = 5;
            this.argCmdLine.Text = "{cmd}";
            this.argCmdLine.Enter += new System.EventHandler(this.startArgs_Enter);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(93, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Working directory:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 48);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Start command:";
            // 
            // argConEmuExe
            // 
            this.argConEmuExe.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.argConEmuExe.Location = new System.Drawing.Point(117, 19);
            this.argConEmuExe.Name = "argConEmuExe";
            this.argConEmuExe.Size = new System.Drawing.Size(402, 20);
            this.argConEmuExe.TabIndex = 1;
            this.argConEmuExe.Text = "ConEmu.exe";
            this.argConEmuExe.Enter += new System.EventHandler(this.startArgs_Enter);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(105, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "ConEmu executable:";
            // 
            // termPanel
            // 
            this.termPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.termPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(43)))), ((int)(((byte)(54)))));
            this.termPanel.Location = new System.Drawing.Point(6, 19);
            this.termPanel.Name = "termPanel";
            this.termPanel.Size = new System.Drawing.Size(564, 330);
            this.termPanel.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.closeBtn);
            this.groupBox2.Controls.Add(this.macroBtn);
            this.groupBox2.Controls.Add(this.printBtn);
            this.groupBox2.Controls.Add(this.promptBox);
            this.groupBox2.Location = new System.Drawing.Point(12, 373);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(576, 48);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Controlling box";
            // 
            // macroBtn
            // 
            this.macroBtn.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.macroBtn.Location = new System.Drawing.Point(413, 17);
            this.macroBtn.Name = "macroBtn";
            this.macroBtn.Size = new System.Drawing.Size(75, 23);
            this.macroBtn.TabIndex = 2;
            this.macroBtn.Text = "Gui&Macro";
            this.macroBtn.UseVisualStyleBackColor = true;
            this.macroBtn.Click += new System.EventHandler(this.macroBtn_Click);
            // 
            // printBtn
            // 
            this.printBtn.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.printBtn.Location = new System.Drawing.Point(332, 17);
            this.printBtn.Name = "printBtn";
            this.printBtn.Size = new System.Drawing.Size(75, 23);
            this.printBtn.TabIndex = 1;
            this.printBtn.Text = "&Print";
            this.printBtn.UseVisualStyleBackColor = true;
            this.printBtn.Click += new System.EventHandler(this.printBtn_Click);
            // 
            // promptBox
            // 
            this.promptBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.promptBox.Location = new System.Drawing.Point(6, 19);
            this.promptBox.Name = "promptBox";
            this.promptBox.Size = new System.Drawing.Size(316, 20);
            this.promptBox.TabIndex = 0;
            this.promptBox.Enter += new System.EventHandler(this.promptBox_Enter);
            this.promptBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.promptBox_KeyDown);
            this.promptBox.Leave += new System.EventHandler(this.promptBox_Leave);
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.DefaultExt = "exe";
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Filter = "*.exe files|*.exe|All files|*.*";
            // 
            // folderBrowserDialog1
            // 
            this.folderBrowserDialog1.Description = "Choose working directory";
            // 
            // closeBtn
            // 
            this.closeBtn.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.closeBtn.Location = new System.Drawing.Point(495, 17);
            this.closeBtn.Name = "closeBtn";
            this.closeBtn.Size = new System.Drawing.Size(75, 23);
            this.closeBtn.TabIndex = 3;
            this.closeBtn.Text = "&Close";
            this.closeBtn.UseVisualStyleBackColor = true;
            this.closeBtn.Click += new System.EventHandler(this.closeBtn_Click);
            // 
            // ChildTerminal
            // 
            this.AcceptButton = this.startBtn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 433);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.MinimumSize = new System.Drawing.Size(40, 39);
            this.Name = "ChildTerminal";
            this.Text = "ConEmu Inside";
            this.Load += new System.EventHandler(this.ChildTerminal_Load);
            this.groupBox1.ResumeLayout(false);
            this.startPanel.ResumeLayout(false);
            this.startPanel.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Panel termPanel;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button macroBtn;
        private System.Windows.Forms.Button printBtn;
        private System.Windows.Forms.TextBox promptBox;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Panel startPanel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button dirBtn;
        private System.Windows.Forms.Button cmdBtn;
        private System.Windows.Forms.Button exeBtn;
        private System.Windows.Forms.CheckBox argRunAs;
        private System.Windows.Forms.TextBox argDirectory;
        private System.Windows.Forms.TextBox argCmdLine;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox argConEmuExe;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button startBtn;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button xmlBtn;
        private System.Windows.Forms.TextBox argXmlFile;
        private System.Windows.Forms.CheckBox argLog;
        private System.Windows.Forms.CheckBox argDebug;
        private System.Windows.Forms.Button closeBtn;
    }
}

