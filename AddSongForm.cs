using System;
using System.Windows.Forms;

namespace Melosoul
{
    public partial class AddSongForm : Form
    {
        public Song ResultSong { get; private set; }

        public AddSongForm()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "MP3 files|*.mp3|All files|*.*";
            dlg.Title = "Chọn file nhạc";
            if (dlg.ShowDialog() == DialogResult.OK)
                txtPath.Text = dlg.FileName;
            dlg.Dispose();
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                MessageBox.Show("Vui lòng nhập tên bài hát!",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPath.Text))
            {
                MessageBox.Show("Vui lòng chọn file MP3!",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            ResultSong = new Song(
                Guid.NewGuid().ToString(),
                txtTitle.Text.Trim(),
                txtArtist.Text.Trim(),
                txtPath.Text.Trim()
            );
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}