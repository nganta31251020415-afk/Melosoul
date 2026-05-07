using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Melosoul.Services;
using WMPLib;

namespace Melosoul
{
    public partial class AddSongDialog : Form
    {
        private static readonly string[] SupportedAudioExtensions =
            { ".mp3", ".mp4", ".wav", ".wma", ".aac", ".flac", ".m4a" };

        public Song ResultSong { get; private set; }

        public AddSongDialog()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = AppText.AudioFileFilter;
                dlg.Title = AppText.SelectMusicTitle;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    if (!IsSupportedAudioFile(dlg.FileName))
                    {
                        MessageBox.Show(AppText.AudioOnlyHint,
                            AppText.InvalidFileTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                    txtPath.Text = dlg.FileName;
                    LoadMetadataFromFile(dlg.FileName);
                }
            }
        }

        private bool IsSupportedAudioFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            foreach (string supported in SupportedAudioExtensions)
            {
                if (extension == supported)
                    return true;
            }
            return false;
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
                MessageBox.Show(AppText.NeedSongTitle,
                    AppText.MissingInfoTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPath.Text))
            {
                MessageBox.Show(AppText.NeedAudioFile,
                    AppText.MissingInfoTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            if (!IsSupportedAudioFile(txtPath.Text.Trim()))
            {
                MessageBox.Show(AppText.AudioOnlyHint,
                    AppText.InvalidFileTitle,
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
