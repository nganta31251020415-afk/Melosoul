using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WMPLib;

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
            {
                txtPath.Text = dlg.FileName;
                LoadMetadataFromFile(dlg.FileName);
            }
            dlg.Dispose();
        }

        private void LoadMetadataFromFile(string filePath)
        {
            try
            {
                var player = new WindowsMediaPlayer();
                var media = player.newMedia(filePath);
                string title = media.getItemInfo("Title");
                string artist = media.getItemInfo("Author");
                if (string.IsNullOrWhiteSpace(artist))
                    artist = media.getItemInfo("WM/AlbumArtist");
                if (string.IsNullOrWhiteSpace(artist))
                    artist = media.getItemInfo("WM/Artist");

                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                    txtTitle.Text = string.IsNullOrWhiteSpace(title)
                        ? System.IO.Path.GetFileNameWithoutExtension(filePath)
                        : title.Trim();

                if (string.IsNullOrWhiteSpace(txtArtist.Text) && !string.IsNullOrWhiteSpace(artist))
                    txtArtist.Text = artist.Trim();

                Marshal.ReleaseComObject(media);
                Marshal.ReleaseComObject(player);
            }
            catch
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                    txtTitle.Text = System.IO.Path.GetFileNameWithoutExtension(filePath);
            }
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