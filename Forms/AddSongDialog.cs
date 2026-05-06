using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WMPLib;

namespace Melosoul
{
    public partial class AddSongDialog : Form
    {
        public Song ResultSong { get; private set; }

        public AddSongDialog()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "MP3 files|*.mp3|All files|*.*";
                dlg.Title = "Chọn file nhạc";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = dlg.FileName;
                    LoadMetadataFromFile(dlg.FileName);
                }
            }
        }

        private void LoadMetadataFromFile(string filePath)
        {
            WindowsMediaPlayer player = null;
            IWMPMedia media = null;
            try
            {
                player = new WindowsMediaPlayer();
                media = player.newMedia(filePath);
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                    txtTitle.Text = System.IO.Path.GetFileNameWithoutExtension(filePath);
            }
            finally
            {
                if (media != null)
                    Marshal.ReleaseComObject(media);
                if (player != null)
                    Marshal.ReleaseComObject(player);
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

