namespace Black
{
    partial class Black2Json
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
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.ConvertFileBtn = new System.Windows.Forms.Button();
            this.timerStatusReset = new System.Windows.Forms.Timer(this.components);
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.checkBoxDecompress = new System.Windows.Forms.CheckBox();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // openFileDialog
            // 
            this.openFileDialog.DefaultExt = "black";
            this.openFileDialog.Filter = "Black File|*.black";
            // 
            // ConvertFileBtn
            // 
            this.ConvertFileBtn.AllowDrop = true;
            this.ConvertFileBtn.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ConvertFileBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ConvertFileBtn.Location = new System.Drawing.Point(12, 12);
            this.ConvertFileBtn.Name = "ConvertFileBtn";
            this.ConvertFileBtn.Size = new System.Drawing.Size(260, 46);
            this.ConvertFileBtn.TabIndex = 1;
            this.ConvertFileBtn.Text = "Click or Drag && Drop";
            this.ConvertFileBtn.UseVisualStyleBackColor = true;
            this.ConvertFileBtn.Click += new System.EventHandler(this.ConvertFileBtn_Click);
            this.ConvertFileBtn.DragDrop += new System.Windows.Forms.DragEventHandler(this.ConvertFileBtn_DragDrop);
            this.ConvertFileBtn.DragEnter += new System.Windows.Forms.DragEventHandler(this.ConvertFileBtn_DragEnter);
            // 
            // timerStatusReset
            // 
            this.timerStatusReset.Interval = 2500;
            this.timerStatusReset.Tick += new System.EventHandler(this.timerStatusReset_Tick);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel,
            this.toolStripStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 84);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(284, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 3;
            this.statusStrip.Text = "statusStrip";
            // 
            // toolStripStatusLabel
            // 
            this.toolStripStatusLabel.Name = "toolStripStatusLabel";
            this.toolStripStatusLabel.Size = new System.Drawing.Size(42, 17);
            this.toolStripStatusLabel.Text = "Status:";
            // 
            // toolStripStatus
            // 
            this.toolStripStatus.Name = "toolStripStatus";
            this.toolStripStatus.Size = new System.Drawing.Size(35, 17);
            this.toolStripStatus.Text = "Idle...";
            // 
            // checkBoxDecompress
            // 
            this.checkBoxDecompress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBoxDecompress.Checked = true;
            this.checkBoxDecompress.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxDecompress.Location = new System.Drawing.Point(12, 64);
            this.checkBoxDecompress.Name = "checkBoxDecompress";
            this.checkBoxDecompress.Size = new System.Drawing.Size(260, 17);
            this.checkBoxDecompress.TabIndex = 2;
            this.checkBoxDecompress.Text = "Create decompressed Json File";
            this.checkBoxDecompress.UseVisualStyleBackColor = true;
            // 
            // Black2Json
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(284, 106);
            this.Controls.Add(this.checkBoxDecompress);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.ConvertFileBtn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.HelpButton = true;
            this.Name = "Black2Json";
            this.Text = "Black2Json v1.2 by Selvin & Andares Sol";
            this.TopMost = true;
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.Button ConvertFileBtn;
        private System.Windows.Forms.Timer timerStatusReset;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatus;
        private System.Windows.Forms.CheckBox checkBoxDecompress;
    }
}

