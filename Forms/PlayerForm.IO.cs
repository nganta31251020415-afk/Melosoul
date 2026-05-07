using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Melosoul.Services;

namespace Melosoul
{
    public partial class PlayerForm
    {
        private void SetLoadingState(bool isLoading)
        {
            btnLoad.Enabled = !isLoading;
            btnAdd.Enabled = !isLoading;
            this.Cursor = isLoading ? Cursors.WaitCursor : Cursors.Default;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (AddSongDialog frm = new AddSongDialog())
            {
                if (frm.ShowDialog() == DialogResult.OK && frm.ResultSong != null)
                {
                    if (PlaylistContainsFile(frm.ResultSong.FilePath))
                    {
                        MessageBox.Show(AppText.DuplicateInPlaylist, AppText.DuplicateTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    _playlist.AddLast(frm.ResultSong);
                    RefreshPlaylistUI();
                    lblStatus.Text = $"Đã thêm: {frm.ResultSong.Title}";
                }
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (dgvPlaylist.SelectedRows.Count == 0)
            {
                MessageBox.Show(AppText.SelectSongToRemove, AppText.NoticeTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var song = GetSongFromRow(dgvPlaylist.SelectedRows[0]);
            if (song == null) return;

            if (MessageBox.Show($"Xóa bài \"{song.Title}\"?", AppText.ConfirmTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _playlist.Remove(song.ID);
                _durationService.Remove(song.FilePath);
                RefreshPlaylistUI();
                lblStatus.Text = $"Đã xóa: {song.Title}";
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (_playlist.Count == 0)
            {
                MessageBox.Show(AppText.PlaylistEmpty, AppText.NoticeTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(AppText.ResetPlaylistConfirm, AppText.ConfirmTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            StopPlayback();
            DisposeAlbumCover();
            _currentAlbumSongId = null;
            lblCurrentSong.Text = "Chưa chọn bài";
            lblArtist.Text = "";
            progressBar.Value = 0;
            lblTimeStart.Text = "0:00";
            lblTimeEnd.Text = "--:--";

            _playlist.Clear();
            _durationService.Clear();
            RefreshPlaylistUI();
            lblStatus.Text = AppText.ResetDone;
        }

        private async void btnLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = AppText.AudioFileFilter,
                Multiselect = true,
                Title = AppText.SelectMusicTitle
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string[] files = dlg.FileNames;
                if (files.Length == 0) return;

                await AddFilesToPlaylistAsync(files);
            }
        }

        private async Task AddFilesToPlaylistAsync(string[] files)
        {
            if (files == null || files.Length == 0) return;

            var existingPaths = _playlist.ToList().Select(s => s.FilePath);

            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            SetLoadingState(true);
            lblStatus.Text = $"Đang load {files.Length} file...";
            lblLoadTime.Text = "Load: đang chạy...";

            try
            {
                var importResult = await _fileImportService.ImportAsync(files, existingPaths);
                var songs = importResult.Songs;
                int duplicateCount = importResult.DuplicateCount;

                foreach (var song in songs)
                    _playlist.AddLast(song);

                RefreshPlaylistUI();
                loadTimer.Stop();
                lblLoadTime.Text = $"Load: {loadTimer.Elapsed.TotalSeconds:0.00} giây";
                lblStatus.Text = duplicateCount > 0
                    ? $"Đã thêm {songs.Count} bài, bỏ qua {duplicateCount} file trùng"
                    : $"Đã thêm {songs.Count} bài";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi load file: " + ex.Message, AppText.ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (loadTimer.IsRunning)
                {
                    loadTimer.Stop();
                    lblLoadTime.Text = $"Load: {loadTimer.Elapsed.TotalSeconds:0.00} giây";
                }

                SetLoadingState(false);
            }
        }

        private void dgvPlaylist_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            e.Effect = files != null && files.Any(IsValidFile)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private async void dgvPlaylist_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            await AddFilesToPlaylistAsync(files);
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (_playlist.Count == 0)
            {
                MessageBox.Show(AppText.PlaylistEmpty, AppText.NoticeTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var folderDlg = new FolderBrowserDialog()
            {
                Description = "Chọn thư mục chứa playlist",
                ShowNewFolderButton = true
            })
            {
                if (folderDlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(folderDlg.SelectedPath))
                    return;

                string defaultFolderName = "Melosoul Playlist";
                if (!PromptForFolderName(defaultFolderName, out string folderName))
                    return;

                string targetFolder = System.IO.Path.Combine(folderDlg.SelectedPath, folderName.Trim());
                if (System.IO.Directory.Exists(targetFolder))
                {
                    if (MessageBox.Show($"Thư mục '{targetFolder}' đã tồn tại. Ghi đè lên file nếu trùng?",
                            AppText.ConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        return;
                    }
                }

                var songs = _playlist.ToList();
                SetLoadingState(true);
                lblStatus.Text = $"Đang lưu playlist vào '{folderName}'...";

                var result = await Task.Run(() => CopyPlaylistFiles(songs, targetFolder));
                SetLoadingState(false);

                if (result.CopiedCount > 0)
                    lblStatus.Text = result.SkippedCount > 0
                        ? $"Đã lưu {result.CopiedCount} bài mp3 vào '{folderName}', bỏ qua {result.SkippedCount} file không phải mp3"
                        : $"Đã lưu {result.CopiedCount} bài mp3 vào '{folderName}'";
                else
                    lblStatus.Text = result.SkippedCount > 0
                        ? "Không có file mp3 nào được lưu."
                        : "Không có bài nào được lưu.";

                if (result.ErrorCount > 0 || result.SkippedCount > 0)
                {
                    MessageBox.Show($"Đã lưu {result.CopiedCount} bài mp3.\n" +
                                    $"Bỏ qua {result.SkippedCount} file không phải mp3.\n" +
                                    $"{result.ErrorCount} file bị lỗi.",
                        AppText.SavePlaylistTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private class SavePlaylistResult
        {
            public int CopiedCount { get; set; }
            public int ErrorCount { get; set; }
            public int SkippedCount { get; set; }
        }

        private SavePlaylistResult CopyPlaylistFiles(System.Collections.Generic.List<Song> songs, string targetFolder)
        {
            var result = new SavePlaylistResult();
            var usedFileNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                System.IO.Directory.CreateDirectory(targetFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                result.ErrorCount = songs.Count;
                return result;
            }

            foreach (var song in songs)
            {
                try
                {
                    if (song == null || string.IsNullOrWhiteSpace(song.FilePath) || !System.IO.File.Exists(song.FilePath))
                    {
                        result.ErrorCount++;
                        continue;
                    }

                    string extension = System.IO.Path.GetExtension(song.FilePath);
                    if (!string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    string baseName = SanitizeFileName(song.Title);
                    if (string.IsNullOrWhiteSpace(baseName))
                        baseName = System.IO.Path.GetFileNameWithoutExtension(song.FilePath);
                    if (string.IsNullOrWhiteSpace(baseName))
                        baseName = "song";

                    string targetFileName = GetUniqueFileName(targetFolder, baseName, ".mp3", usedFileNames);
                    string targetPath = System.IO.Path.Combine(targetFolder, targetFileName);

                    System.IO.File.Copy(song.FilePath, targetPath, true);
                    result.CopiedCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                    result.ErrorCount++;
                }
            }

            return result;
        }

        private string GetUniqueFileName(string folder, string baseName, string extension,
            System.Collections.Generic.HashSet<string> usedFileNames)
        {
            string cleanBaseName = SanitizeFileName(baseName);
            if (string.IsNullOrWhiteSpace(cleanBaseName))
                cleanBaseName = "song";

            string fileName = cleanBaseName + extension;
            int index = 2;

            while (usedFileNames.Contains(fileName) ||
                   System.IO.File.Exists(System.IO.Path.Combine(folder, fileName)))
            {
                fileName = $"{cleanBaseName} ({index}){extension}";
                index++;
            }

            usedFileNames.Add(fileName);
            return fileName;
        }

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim().TrimEnd('.');
        }

        private bool PromptForFolderName(string defaultName, out string folderName)
        {
            folderName = defaultName;

            using (var form = new Form())
            using (var label = new Label())
            using (var textBox = new TextBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = "Đặt tên playlist";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(380, 125);

                label.Text = "Tên folder:";
                label.AutoSize = true;
                label.Location = new Point(12, 18);

                textBox.Text = defaultName;
                textBox.Location = new Point(88, 15);
                textBox.Width = 275;
                textBox.SelectAll();

                okButton.Text = "OK";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(207, 78);
                okButton.Width = 75;

                cancelButton.Text = "Hủy";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(288, 78);
                cancelButton.Width = 75;

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                while (form.ShowDialog(this) == DialogResult.OK)
                {
                    string cleanedName = SanitizeFileName(textBox.Text);
                    if (!string.IsNullOrWhiteSpace(cleanedName))
                    {
                        folderName = cleanedName;
                        return true;
                    }

                    MessageBox.Show(AppText.FolderNameEmpty, AppText.NoticeTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return false;
        }
    }
}
