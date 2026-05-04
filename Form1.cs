using System;
using System.Drawing;
using System.Windows.Forms;
using WMPLib;
using System.Runtime.InteropServices;
namespace Melosoul
{
    public partial class Form1 : Form
    {
        private DoublyLinkedList _playlist = new DoublyLinkedList();
        private WindowsMediaPlayer _wmp;
        private bool _isFirstPlay = true;
        private readonly string _autoSavePath =
    System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Melosoul", "autosave.txt");

        [System.Runtime.InteropServices.DllImport("uxtheme.dll",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string app, string id);

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr,
            ref int attrValue, int attrSize);

        public Form1()
        {
            InitializeComponent();
            InitWMP();
        }

        private void InitWMP()
        {
            _wmp = new WindowsMediaPlayer();
            _wmp.uiMode = "invisible";
            _wmp.settings.autoStart = true;
            _wmp.settings.volume = 80;
            _wmp.PlayStateChange += new _WMPOCXEvents_PlayStateChangeEventHandler(WMP_PlayStateChange);
            _wmp.MediaError += new _WMPOCXEvents_MediaErrorEventHandler(WMP_MediaError);
        }

        private void WMP_PlayStateChange(int NewState)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (_isFirstPlay) { _isFirstPlay = false; return; }
            if (NewState == (int)WMPPlayState.wmppsMediaEnded)
            {
                if (InvokeRequired)
                    Invoke(new Action(AutoNext));
                else
                    AutoNext();
            }
        }

        private void WMP_MediaError(object pMediaObject)
        {
            if (InvokeRequired)
                Invoke(new Action(() => MessageBox.Show(
                    "WMP không thể phát file này.",
                    "Lỗi Media", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            else
                MessageBox.Show("WMP không thể phát file này.",
                    "Lỗi Media", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void AutoNext()
        {
            int maxSkip = _playlist.Count;
            int skipped = 0;
            while (true)
            {
                Song song = _playlist.Next();
                if (song == null) { lblCurrentSong.Text = "Đã phát hết playlist."; return; }
                if (IsValidFile(song)) { PlaySong(song); RefreshPlaylistUI(); return; }
                skipped++;
                if (skipped >= maxSkip) { lblCurrentSong.Text = "Không có file hợp lệ."; return; }
            }
        }

        private void AutoSave()
        {
            try
            {
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(_autoSavePath));
                var lines = new System.Collections.Generic.List<string>();
                foreach (var s in _playlist.ToList())
                    lines.Add($"{s.Title}|{s.Artist}|{s.FilePath}");
                System.IO.File.WriteAllLines(_autoSavePath, lines);
            }
            catch { }
        }

        private void AutoLoad()
        {
            try
            {
                if (!System.IO.File.Exists(_autoSavePath)) return;
                foreach (var line in System.IO.File.ReadAllLines(_autoSavePath))
                {
                    var p = line.Split('|');
                    if (p.Length < 3) continue;
                    _playlist.AddLast(new Song(
                        Guid.NewGuid().ToString(),
                        p[0].Trim(), p[1].Trim(), p[2].Trim()));
                }
                RefreshPlaylistUI();
            }
            catch { }
        }


        private void PlaySong(Song song)
        {
            if (song == null) return;
            if (!System.IO.File.Exists(song.FilePath))
            {
                MessageBox.Show("File không tồn tại:\n" + song.FilePath,
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                _isFirstPlay = true;
                _wmp.URL = song.FilePath;
                lblCurrentSong.Text = song.Title;
                lblArtist.Text = song.Artist;
                lblStatus.Text = $"🎵 Đang phát: {song.Title}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể phát:\n" + ex.Message,
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopPlayback()
        {
            try { _wmp.controls.stop(); _wmp.URL = ""; }
            catch { }
        }

        private string GetDuration(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return "--:--";

                var player = new WindowsMediaPlayer();
                var media = player.newMedia(filePath);

                // Chờ WMP load xong
                int retry = 0;
                while (media.duration <= 0 && retry < 50)
                {
                    System.Threading.Thread.Sleep(100);
                    retry++;
                }

                double dur = media.duration;
                System.Runtime.InteropServices.Marshal.ReleaseComObject(player);

                if (dur <= 0) return "--:--";
                int min = (int)(dur / 60);
                int sec = (int)(dur % 60);
                return $"{min}:{sec:D2}";
            }
            catch { return "--:--"; }
        }

        private bool IsValidFile(Song song)
        {
            if (song == null) return false;
            if (!System.IO.File.Exists(song.FilePath)) return false;
            string ext = System.IO.Path.GetExtension(song.FilePath).ToLowerInvariant();
            string[] supported = { ".mp3", ".mp4", ".wav", ".wma", ".aac", ".flac", ".m4a" };
            return Array.Exists(supported, e => e == ext);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            int color = unchecked((int)0xFFC8AFFF);
            DwmSetWindowAttribute(this.Handle, 35, ref color, sizeof(int));

            SetWindowTheme(progressBar.Handle, "", "");

            listBoxPlaylist.EnableHeadersVisualStyles = false;
            listBoxPlaylist.AllowUserToAddRows = false;
            listBoxPlaylist.RowHeadersVisible = false;
            listBoxPlaylist.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            listBoxPlaylist.GridColor = Color.FromArgb(45, 25, 38);
            listBoxPlaylist.BackgroundColor = Color.FromArgb(18, 18, 18);
            listBoxPlaylist.BorderStyle = BorderStyle.None;

            listBoxPlaylist.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 15, 28);
            listBoxPlaylist.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(200, 130, 160);
            listBoxPlaylist.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            listBoxPlaylist.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            listBoxPlaylist.DefaultCellStyle.BackColor = Color.FromArgb(18, 18, 18);
            listBoxPlaylist.DefaultCellStyle.ForeColor = Color.FromArgb(204, 204, 204);
            listBoxPlaylist.DefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 30, 60);
            listBoxPlaylist.DefaultCellStyle.SelectionForeColor = Color.FromArgb(255, 255, 255);
            listBoxPlaylist.DefaultCellStyle.Font = new Font("Segoe UI", 9f);

            listBoxPlaylist.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(22, 22, 22);
            listBoxPlaylist.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(204, 204, 204);
            listBoxPlaylist.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 30, 60);
            listBoxPlaylist.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(255, 255, 255);

            colNum.SortMode = DataGridViewColumnSortMode.NotSortable;
            colTitle.SortMode = DataGridViewColumnSortMode.NotSortable;
            colArtist.SortMode = DataGridViewColumnSortMode.NotSortable;
            colDuration.SortMode = DataGridViewColumnSortMode.NotSortable;

            AutoLoad();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            AutoSave();
            if (_wmp != null)
            {
                try
                {
                    _wmp.PlayStateChange -= WMP_PlayStateChange;
                    _wmp.MediaError -= WMP_MediaError;
                    _wmp.controls.stop();
                    Marshal.ReleaseComObject(_wmp);
                }
                catch { }
                finally { _wmp = null; }
            }
            _playlist.Dispose();
            base.OnFormClosing(e);
        }

        private void RefreshPlaylistUI()
        {
            var list = _playlist.ToList();
            listBoxPlaylist.Rows.Clear();
            for (int i = 0; i < list.Count; i++)
                listBoxPlaylist.Rows.Add(i + 1, list[i].Title, list[i].Artist,
                    GetDuration(list[i].FilePath));
            lblSongCount.Text = $"{_playlist.Count} bài hát";
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddSongForm frm = new AddSongForm();
            if (frm.ShowDialog() == DialogResult.OK && frm.ResultSong != null)
            {
                _playlist.AddLast(frm.ResultSong);
                RefreshPlaylistUI();
                lblStatus.Text = $"Đã thêm: {frm.ResultSong.Title}";
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (listBoxPlaylist.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn bài muốn xóa!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string title = listBoxPlaylist.SelectedRows[0].Cells["colTitle"].Value?.ToString();
            var song = _playlist.ToList().Find(s => s.Title == title);
            if (song == null) return;

            if (MessageBox.Show($"Xóa bài \"{song.Title}\"?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _playlist.Remove(song.ID);
                RefreshPlaylistUI();
                lblStatus.Text = $"Đã xóa: {song.Title}";
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Text files|*.txt" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                _playlist.Clear();
                foreach (var line in System.IO.File.ReadAllLines(dlg.FileName))
                {
                    var p = line.Split('|');
                    if (p.Length < 3) continue;
                    _playlist.AddLast(new Song(
                        Guid.NewGuid().ToString(), // ID
                        p[0].Trim(),               // Title
                        p[1].Trim(),               // Artist
                        p[2].Trim()                // FilePath
                    ));
                }
                RefreshPlaylistUI();
                lblStatus.Text = $"Đã load {_playlist.Count} bài";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (_playlist.Count == 0)
            {
                MessageBox.Show("Playlist đang rỗng!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog
            { Filter = "Text files|*.txt", FileName = "playlist.txt" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (var s in _playlist.ToList())
                    lines.Add($"{s.Title}|{s.Artist}|{s.FilePath}");
                System.IO.File.WriteAllLines(dlg.FileName, lines);
                lblStatus.Text = $"Đã lưu {_playlist.Count} bài";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (_wmp.playState == WMPPlayState.wmppsPaused)
            {
                _wmp.controls.play();
                return;
            }
            Song song = _playlist.CurrentSong;
            if (song == null)
                MessageBox.Show("Playlist trống.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                PlaySong(song);
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            Song song = _playlist.Next();
            if (song == null)
                MessageBox.Show("Đã hết playlist.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            else { PlaySong(song); RefreshPlaylistUI(); }
        }

        private void btnPrev_Click(object sender, EventArgs e)
        {
            Song song = _playlist.Prev();
            if (song == null)
                MessageBox.Show("Đây là bài đầu tiên.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            else { PlaySong(song); RefreshPlaylistUI(); }
        }

        private void Shuffle_CheckedChanged(object sender, EventArgs e)
        {
            if (Shuffle.Checked)
            {
                _playlist.Shuffle();
                RefreshPlaylistUI();
                lblStatus.Text = "Đã xáo trộn playlist";
            }
        }
        private void btnSort_Click(object sender, EventArgs e)
        {
            _playlist.Sort();
            RefreshPlaylistUI();
            lblStatus.Text = "Đã sắp xếp A→Z";
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            string kw = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(kw)) { RefreshPlaylistUI(); return; }
            var kq = _playlist.Find(kw);
            listBoxPlaylist.Rows.Clear();
            for (int i = 0; i < kq.Count; i++)
                listBoxPlaylist.Rows.Add(i + 1, kq[i].Title, kq[i].Artist, "--:--");
        }

        private void chkRepeatAll_CheckedChanged(object sender, EventArgs e)
        {
            _playlist.IsRepeatAll = chkRepeatAll.Checked;
        }

        private void listBoxPlaylist_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string title = listBoxPlaylist.Rows[e.RowIndex].Cells["colTitle"].Value?.ToString();
            var song = _playlist.ToList().Find(s => s.Title == title);
            if (song == null) return;
            _playlist.MoveTo(song.ID);
            PlaySong(song);
        }
    }
}