using System;
using System.Drawing;
using System.Windows.Forms;

namespace Melosoul
{
    public partial class RenameSongDialog : Form
    {
        public string SongTitle { get; set; }
        public string SongArtist { get; set; }

        public RenameSongDialog(string currentTitle, string currentArtist)
        {
            InitializeComponent();
            SongTitle = currentTitle;
            SongArtist = currentArtist;
            txtTitle.Text = currentTitle;
            txtArtist.Text = currentArtist;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.FromArgb(200, 200, 200);
            this.Font = new Font("Segoe UI", 9f);

            lblTitle.ForeColor = Color.FromArgb(200, 130, 160);
            lblTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            txtTitle.BackColor = Color.FromArgb(50, 50, 50);
            txtTitle.ForeColor = Color.FromArgb(220, 220, 220);
            txtTitle.Font = new Font("Segoe UI", 9f);
            txtTitle.BorderStyle = BorderStyle.FixedSingle;

            txtArtist.BackColor = Color.FromArgb(50, 50, 50);
            txtArtist.ForeColor = Color.FromArgb(220, 220, 220);
            txtArtist.Font = new Font("Segoe UI", 9f);
            txtArtist.BorderStyle = BorderStyle.FixedSingle;

            btnOK.BackColor = Color.FromArgb(80, 30, 60);
            btnOK.ForeColor = Color.FromArgb(255, 175, 200);
            btnOK.FlatStyle = FlatStyle.Flat;
            btnOK.FlatAppearance.BorderColor = Color.FromArgb(100, 50, 80);
            btnOK.Font = new Font("Segoe UI", 9f);

            btnCancel.BackColor = Color.FromArgb(50, 50, 50);
            btnCancel.ForeColor = Color.FromArgb(180, 180, 180);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btnCancel.Font = new Font("Segoe UI", 9f);
        }

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.txtTitle = new System.Windows.Forms.TextBox();
            this.lblArtist = new System.Windows.Forms.Label();
            this.txtArtist = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(12, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(102, 15);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Tên bài hát:";

            // txtTitle
            this.txtTitle.Location = new System.Drawing.Point(12, 45);
            this.txtTitle.Name = "txtTitle";
            this.txtTitle.Size = new System.Drawing.Size(360, 28);
            this.txtTitle.TabIndex = 1;
            this.txtTitle.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TxtTitle_KeyDown);

            // lblArtist
            this.lblArtist.AutoSize = true;
            this.lblArtist.Location = new System.Drawing.Point(12, 82);
            this.lblArtist.Name = "lblArtist";
            this.lblArtist.Size = new System.Drawing.Size(56, 15);
            this.lblArtist.TabIndex = 2;
            this.lblArtist.Text = "Nghệ sĩ:";

            // txtArtist
            this.txtArtist.Location = new System.Drawing.Point(12, 107);
            this.txtArtist.Name = "txtArtist";
            this.txtArtist.Size = new System.Drawing.Size(360, 28);
            this.txtArtist.TabIndex = 3;
            this.txtArtist.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TxtTitle_KeyDown);

            // btnOK
            this.btnOK.Location = new System.Drawing.Point(200, 147);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(80, 32);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOK_Click);

            // btnCancel
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(292, 147);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(80, 32);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Hủy";
            this.btnCancel.UseVisualStyleBackColor = true;

            // RenameSongDialog
            this.AutoScaleDimensions = new System.Drawing.SizeF(7f, 15f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(384, 194);
            this.ControlBox = true;
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.txtTitle);
            this.Controls.Add(this.lblArtist);
            this.Controls.Add(this.txtArtist);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RenameSongDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Đổi tên bài hát";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void TxtTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                e.SuppressKeyPress = true;
                BtnOK_Click(null, null);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            SongTitle = txtTitle.Text.Trim();
            SongArtist = txtArtist.Text.Trim();
            if (string.IsNullOrWhiteSpace(SongTitle))
            {
                MessageBox.Show("Tên bài hát không được để trống.", "Lỗi", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTitle.Focus();
                return;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.Label lblArtist;
        private System.Windows.Forms.TextBox txtArtist;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}


