using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WMPLib;
using System.Runtime.InteropServices;
namespace Melosoul
{
    public partial class Form1 : Form
    {
        private DoublyLinkedList _playlist = new DoublyLinkedList();
        private WindowsMediaPlayer _wmp;
        private Timer _playbackTimer;
        private Timer _playlistClickTimer;
        private DataGridViewCellMouseEventArgs _pendingPlaylistClick;
        private bool _isFirstPlay = true;
        private string _currentAlbumSongId;
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
            InitializePlaybackTimer();
            InitializePlaylistClickTimer();
        }

        private void InitWMP()
        {
            _wmp = new WindowsMediaPlayer();
            _wmp.uiMode = "invisible";
            _wmp.settings.autoStart = true;
            _wmp.settings.volume = trackVolume.Value;
            _wmp.PlayStateChange += new _WMPOCXEvents_PlayStateChangeEventHandler(WMP_PlayStateChange);
            _wmp.MediaError += new _WMPOCXEvents_MediaErrorEventHandler(WMP_MediaError);
        }

        private void InitializePlaybackTimer()
        {
            _playbackTimer = new Timer { Interval = 500 };
            _playbackTimer.Tick += PlaybackTimer_Tick;
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            UpdatePlaybackProgress();
        }

        private void UpdatePlaybackProgress()
        {
            if (_wmp == null || _wmp.currentMedia == null)
            {
                progressBar.Value = 0;
                lblTimeStart.Text = "0:00";
                lblTimeEnd.Text = "--:--";
                return;
            }

            double duration = 0;
            double position = 0;
            try
            {
                duration = _wmp.currentMedia.duration;
                position = _wmp.controls.currentPosition;
            }
            catch
            {
                duration = 0;
                position = 0;
            }

            lblTimeStart.Text = FormatTime(position);
            lblTimeEnd.Text = duration > 0 ? FormatTime(duration) : "--:--";

            if (duration > 0)
            {
                int percent = (int)Math.Round((position / duration) * 100);
                progressBar.Value = Math.Min(100, Math.Max(0, percent));
            }
            else
            {
                progressBar.Value = 0;
            }
        }

        private string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int min = (int)seconds / 60;
            int sec = (int)seconds % 60;
            return $"{min}:{sec:D2}";
        }

        private void SetPlayButtonState(bool isPlaying)
        {
            btnPlay.Text = isPlaying ? "⏸" : "▶";
            if (isPlaying)
                _playbackTimer?.Start();
            else
                _playbackTimer?.Stop();
        }

        private void progressBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (_wmp == null || _wmp.currentMedia == null || _wmp.currentMedia.duration <= 0)
                return;

            if (e.Button != MouseButtons.Left)
                return;

            double ratio = Math.Max(0, Math.Min(1, (double)e.X / progressBar.Width));
            double target = _wmp.currentMedia.duration * ratio;

            try
            {
                _wmp.controls.currentPosition = target;
                UpdatePlaybackProgress();
            }
            catch { }
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
                return;
            }

            if (NewState == (int)WMPPlayState.wmppsPlaying)
            {
                if (InvokeRequired)
                    Invoke(new Action(() => SetPlayButtonState(true)));
                else
                    SetPlayButtonState(true);
            }
            else if (NewState == (int)WMPPlayState.wmppsPaused || NewState == (int)WMPPlayState.wmppsStopped)
            {
                if (InvokeRequired)
                    Invoke(new Action(() => SetPlayButtonState(false)));
                else
                    SetPlayButtonState(false);
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

        private void trackVolume_ValueChanged(object sender, EventArgs e)
        {
            if (_wmp != null)
                _wmp.settings.volume = trackVolume.Value;
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
                UpdateAlbumCover(song);
                SetPlayButtonState(true);
                UpdatePlaylistSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể phát:\n" + ex.Message,
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopPlayback()
        {
            try
            {
                _wmp.controls.stop();
                _wmp.URL = "";
                SetPlayButtonState(false);
            }
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
                System.Runtime.InteropServices.Marshal.ReleaseComObject(media);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(player);

                if (dur <= 0) return "--:--";
                int min = (int)(dur / 60);
                int sec = (int)(dur % 60);
                return $"{min}:{sec:D2}";
            }
            catch { return "--:--"; }
        }

        private Image _albumCover;

        private bool IsValidFile(Song song)
        {
            if (song == null) return false;
            if (string.IsNullOrWhiteSpace(song.FilePath)) return false;
            if (!System.IO.File.Exists(song.FilePath)) return false;
            string ext = System.IO.Path.GetExtension(song.FilePath).ToLowerInvariant();
            string[] supported = { ".mp3", ".mp4", ".wav", ".wma", ".aac", ".flac", ".m4a" };
            return Array.Exists(supported, e => e == ext);
        }

        private bool IsValidFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            if (!System.IO.File.Exists(filePath)) return false;
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            string[] supported = { ".mp3", ".mp4", ".wav", ".wma", ".aac", ".flac", ".m4a" };
            return Array.Exists(supported, e => e == ext);
        }

        private bool PlaylistContainsFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            return _playlist.ToList().Any(s =>
                string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        private Song CreateSongFromFile(string filePath)
        {
            try
            {
                string title = null;
                string artist = null;
                var player = new WindowsMediaPlayer();
                var media = player.newMedia(filePath);
                title = media.getItemInfo("Title");
                artist = media.getItemInfo("Author");
                if (string.IsNullOrWhiteSpace(artist))
                    artist = media.getItemInfo("WM/AlbumArtist");
                if (string.IsNullOrWhiteSpace(artist))
                    artist = media.getItemInfo("WM/Artist");
                if (string.IsNullOrWhiteSpace(title))
                    title = System.IO.Path.GetFileNameWithoutExtension(filePath);

                Marshal.ReleaseComObject(media);
                Marshal.ReleaseComObject(player);

                return new Song(Guid.NewGuid().ToString(), title.Trim(), artist?.Trim() ?? "", filePath);
            }
            catch
            {
                string title = System.IO.Path.GetFileNameWithoutExtension(filePath);
                return new Song(Guid.NewGuid().ToString(), title, string.Empty, filePath);
            }
        }

        private void DisposeAlbumCover()
        {
            if (_albumCover != null)
            {
                picAlbum.Image = null;
                _albumCover.Dispose();
                _albumCover = null;
            }
        }

        private string FindCoverImagePath(Song song)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.FilePath))
                return null;

            string dir = System.IO.Path.GetDirectoryName(song.FilePath);
            if (string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir))
                return null;

            string fileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(song.FilePath);
            string[] exactCandidates = new[]
            {
                $"{fileNameNoExt}.jpg",
                $"{fileNameNoExt}.jpeg",
                $"{fileNameNoExt}.png",
                $"{fileNameNoExt}.bmp",
                $"{fileNameNoExt}.gif"
            };

            foreach (var candidate in exactCandidates)
            {
                string path = System.IO.Path.Combine(dir, candidate);
                if (System.IO.File.Exists(path))
                    return path;
            }

            string normalizedTarget = NormalizeName(fileNameNoExt);
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            foreach (var file in System.IO.Directory.EnumerateFiles(dir))
            {
                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (!extensions.Contains(ext))
                    continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                string normalizedName = NormalizeName(name);
                if (normalizedName.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedName))
                    return file;
            }

            string[] fallbackCandidates = new[]
            {
                "cover.jpg",
                "cover.jpeg",
                "cover.png",
                "cover.bmp",
                "cover.gif",
                "folder.jpg",
                "folder.jpeg",
                "folder.png",
                "folder.bmp",
                "folder.gif",
                "album.jpg",
                "album.jpeg",
                "album.png",
                "album.bmp",
                "album.gif",
                "front.jpg",
                "front.jpeg",
                "front.png",
                "front.bmp",
                "front.gif"
            };

            foreach (var candidate in fallbackCandidates)
            {
                string path = System.IO.Path.Combine(dir, candidate);
                if (System.IO.File.Exists(path))
                    return path;
            }

            return null;
        }

        private void UpdateAlbumCover(Song song)
        {
            if (picAlbum == null)
                return;

            DisposeAlbumCover();
            picAlbum.Image = null;

            if (song == null || string.IsNullOrWhiteSpace(song.FilePath))
                return;

            _currentAlbumSongId = song.ID;

            Task.Run(() =>
            {
                try
                {
                    if (song.ID != _currentAlbumSongId)
                        return;

                    Bitmap loadedImage = CreateAlbumCoverImage(song);
                    if (loadedImage == null)
                        return;

                    if (song.ID != _currentAlbumSongId)
                    {
                        loadedImage.Dispose();
                        return;
                    }

                    if (picAlbum.InvokeRequired)
                    {
                        picAlbum.Invoke(new Action(() =>
                        {
                            if (song.ID == _currentAlbumSongId)
                            {
                                DisposeAlbumCover();
                                _albumCover = loadedImage;
                                picAlbum.Image = _albumCover;
                            }
                            else
                            {
                                loadedImage.Dispose();
                            }
                        }));
                    }
                    else
                    {
                        DisposeAlbumCover();
                        _albumCover = loadedImage;
                        picAlbum.Image = _albumCover;
                    }
                }
                catch
                {
                }
            });
        }

        private Bitmap CreateAlbumCoverImage(Song song)
        {
            Bitmap embeddedImage = CreateEmbeddedMp3Image(song?.FilePath, Math.Max(picAlbum.Width, picAlbum.Height));
            if (embeddedImage != null)
                return embeddedImage;

            string path = FindSongSpecificCoverImagePath(song);
            if (path == null)
                return null;

            try
            {
                using (var fs = System.IO.File.OpenRead(path))
                using (var img = Image.FromStream(fs))
                {
                    return ResizeImageToFit(img, Math.Max(picAlbum.Width, picAlbum.Height));
                }
            }
            catch
            {
                return null;
            }
        }

        private Bitmap CreateEmbeddedMp3Image(string filePath, int maxSize)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return null;

            if (!string.Equals(System.IO.Path.GetExtension(filePath), ".mp3", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                byte[] imageBytes = ReadEmbeddedMp3ImageBytes(filePath);
                if (imageBytes == null || imageBytes.Length == 0)
                    return null;

                using (var ms = new System.IO.MemoryStream(imageBytes))
                using (var img = Image.FromStream(ms))
                {
                    return ResizeImageToFit(img, maxSize);
                }
            }
            catch
            {
                return null;
            }
        }

        private Bitmap ResizeImageToFit(Image image, int maxSize)
        {
            if (image.Width > maxSize || image.Height > maxSize)
            {
                float ratio = Math.Min((float)maxSize / image.Width, (float)maxSize / image.Height);
                int newWidth = Math.Max(1, (int)(image.Width * ratio));
                int newHeight = Math.Max(1, (int)(image.Height * ratio));
                return new Bitmap(image, newWidth, newHeight);
            }

            return new Bitmap(image);
        }

        private string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var cleaned = new System.Text.StringBuilder();
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    cleaned.Append(c);
            }
            return cleaned.ToString();
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
            listBoxPlaylist.MultiSelect = false;
            listBoxPlaylist.ClearSelection();

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
            colImage.SortMode = DataGridViewColumnSortMode.NotSortable;
            colImage.Visible = false;

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
            DisposePlaylistImages();
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space && !(ActiveControl is TextBox))
            {
                TogglePlayPause();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshPlaylistUI()
        {
            var list = _playlist.ToList();
            DisposePlaylistImages();
            listBoxPlaylist.Rows.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var rowIndex = listBoxPlaylist.Rows.Add(i + 1, list[i].Title, list[i].Artist, "--:--", null);
                listBoxPlaylist.Rows[rowIndex].Tag = list[i].ID;
            }
            lblSongCount.Text = $"{_playlist.Count} bài hát";
            UpdatePlaylistDurationsAsync(list);
            UpdatePlaylistImagesAsync(list);
            UpdatePlaylistSelection();
        }

        private void UpdatePlaylistSelection()
        {
            if (listBoxPlaylist.IsDisposed) return;

            if (listBoxPlaylist.InvokeRequired)
            {
                listBoxPlaylist.Invoke(new Action(UpdatePlaylistSelection));
                return;
            }

            listBoxPlaylist.ClearSelection();
            listBoxPlaylist.CurrentCell = null;

            var current = _playlist.CurrentSong;
            if (current == null)
                return;

            var list = _playlist.ToList();
            int index = list.FindIndex(s => s.ID == current.ID);
            if (index < 0 || index >= listBoxPlaylist.Rows.Count)
                return;

            var row = listBoxPlaylist.Rows[index];
            row.Selected = true;
            if (listBoxPlaylist.Columns.Contains("colTitle"))
            {
                listBoxPlaylist.CurrentCell = row.Cells["colTitle"];
            }
            else if (listBoxPlaylist.Columns.Count > 0)
            {
                listBoxPlaylist.CurrentCell = row.Cells[0];
            }

            try
            {
                listBoxPlaylist.FirstDisplayedScrollingRowIndex = index;
            }
            catch { }
        }

        private void InitializePlaylistClickTimer()
        {
            _playlistClickTimer = new Timer();
            _playlistClickTimer.Interval = Math.Min(SystemInformation.DoubleClickTime, 200);
            _playlistClickTimer.Tick += (sender, e) =>
            {
                _playlistClickTimer.Stop();
                if (_pendingPlaylistClick == null) return;
                HandlePlaylistSingleClick(_pendingPlaylistClick);
                _pendingPlaylistClick = null;
            };
        }

        private void listBoxPlaylist_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.Button != MouseButtons.Left) return;
            _pendingPlaylistClick = e;
            _playlistClickTimer.Stop();
            _playlistClickTimer.Start();
        }

        private void listBoxPlaylist_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            _playlistClickTimer.Stop();
            _pendingPlaylistClick = null;

            if (e.RowIndex < 0) return;
            if (listBoxPlaylist.Rows.Count <= e.RowIndex) return;

            var row = listBoxPlaylist.Rows[e.RowIndex];
            var songId = row.Tag?.ToString();
            if (string.IsNullOrEmpty(songId)) return;

            var song = _playlist.ToList().Find(s => s.ID == songId);
            if (song == null) return;

            var oldTitle = song.Title;
            using (var dialog = new RenameSongForm(oldTitle))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string newTitle = dialog.SongTitle;
                    if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != oldTitle)
                    {
                        song.Title = newTitle;
                        row.Cells["colTitle"].Value = newTitle;

                        if (_playlist.CurrentSong != null && _playlist.CurrentSong.ID == song.ID)
                        {
                            lblCurrentSong.Text = newTitle;
                        }
                    }
                }
            }
        }

        private void HandlePlaylistSingleClick(DataGridViewCellMouseEventArgs e)
        {
            if (listBoxPlaylist.IsDisposed) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (e.RowIndex >= listBoxPlaylist.Rows.Count) return;
            var row = listBoxPlaylist.Rows[e.RowIndex];
            var songId = row.Tag?.ToString();
            if (string.IsNullOrEmpty(songId)) return;
            var song = _playlist.ToList().Find(s => s.ID == songId);
            if (song == null) return;

            _playlist.MoveTo(song.ID);
            PlaySong(song);
            UpdatePlaylistSelection();
        }

        private void listBoxPlaylist_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // Removed: using popup dialog instead of inline edit
        }

        private void listBoxPlaylist_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            // Removed: using popup dialog instead of inline edit
        }

        private void UpdatePlaylistDurationsAsync(System.Collections.Generic.List<Song> list)
        {
            Task.Run(() =>
            {
                var durationMap = new System.Collections.Generic.Dictionary<string, string>();
                foreach (var song in list)
                {
                    string duration = GetDuration(song.FilePath);
                    durationMap[song.ID] = duration;
                }

                if (listBoxPlaylist.IsDisposed) return;

                if (listBoxPlaylist.InvokeRequired)
                {
                    listBoxPlaylist.Invoke(new Action(() =>
                    {
                        UpdateDurationsInUI(durationMap);
                    }));
                }
                else
                {
                    UpdateDurationsInUI(durationMap);
                }
            });
        }

        private void UpdateDurationsInUI(System.Collections.Generic.Dictionary<string, string> durationMap)
        {
            for (int i = 0; i < listBoxPlaylist.Rows.Count; i++)
            {
                var row = listBoxPlaylist.Rows[i];
                string title = row.Cells["colTitle"].Value?.ToString();
                if (string.IsNullOrEmpty(title)) continue;

                var song = _playlist.ToList().Find(s => s.Title == title);
                if (song != null && durationMap.TryGetValue(song.ID, out string duration))
                {
                    row.Cells["colDuration"].Value = duration;
                }
            }
        }

        private void UpdatePlaylistImagesAsync(System.Collections.Generic.List<Song> list)
        {
            if (listBoxPlaylist == null ||
                !listBoxPlaylist.Columns.Contains("colImage") ||
                !listBoxPlaylist.Columns["colImage"].Visible)
            {
                return;
            }

            Task.Run(() =>
            {
                var imageMap = new System.Collections.Generic.Dictionary<string, Image>();
                foreach (var song in list)
                {
                    var image = CreatePlaylistThumbnail(song);
                    if (image != null)
                        imageMap[song.ID] = image;
                }

                if (listBoxPlaylist.IsDisposed)
                {
                    DisposeImages(imageMap.Values);
                    return;
                }

                Action updateAction = () =>
                {
                    try
                    {
                        UpdateImagesInUI(imageMap);
                    }
                    catch
                    {
                        DisposeImages(imageMap.Values);
                    }
                };

                if (listBoxPlaylist.InvokeRequired)
                    listBoxPlaylist.Invoke(updateAction);
                else
                    updateAction();
            });
        }

        private void UpdateImagesInUI(System.Collections.Generic.Dictionary<string, Image> imageMap)
        {
            var usedImages = new System.Collections.Generic.HashSet<Image>();
            for (int i = 0; i < listBoxPlaylist.Rows.Count; i++)
            {
                var row = listBoxPlaylist.Rows[i];
                string songId = row.Tag?.ToString();
                if (string.IsNullOrEmpty(songId)) continue;

                if (imageMap.TryGetValue(songId, out Image image))
                {
                    var oldImage = row.Cells["colImage"].Value as Image;
                    row.Cells["colImage"].Value = image;
                    usedImages.Add(image);
                    oldImage?.Dispose();
                }
            }

            foreach (var pair in imageMap)
            {
                if (!usedImages.Contains(pair.Value))
                    pair.Value.Dispose();
            }
        }

        private Image CreatePlaylistThumbnail(Song song)
        {
            var embeddedImage = CreateEmbeddedMp3Thumbnail(song?.FilePath);
            if (embeddedImage != null)
                return embeddedImage;

            string path = FindSongSpecificCoverImagePath(song);
            if (path == null)
                return null;

            try
            {
                using (var fs = System.IO.File.OpenRead(path))
                using (var img = Image.FromStream(fs))
                {
                    return new Bitmap(img, 36, 36);
                }
            }
            catch
            {
                return null;
            }
        }

        private Image CreateEmbeddedMp3Thumbnail(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return null;

            if (!string.Equals(System.IO.Path.GetExtension(filePath), ".mp3", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                byte[] imageBytes = ReadEmbeddedMp3ImageBytes(filePath);
                if (imageBytes == null || imageBytes.Length == 0)
                    return null;

                using (var ms = new System.IO.MemoryStream(imageBytes))
                using (var img = Image.FromStream(ms))
                {
                    return new Bitmap(img, 36, 36);
                }
            }
            catch
            {
                return null;
            }
        }

        private byte[] ReadEmbeddedMp3ImageBytes(string filePath)
        {
            using (var fs = System.IO.File.OpenRead(filePath))
            {
                if (fs.Length < 10)
                    return null;

                byte[] header = new byte[10];
                if (fs.Read(header, 0, header.Length) != header.Length)
                    return null;

                if (header[0] != 'I' || header[1] != 'D' || header[2] != '3')
                    return null;

                int majorVersion = header[3];
                int flags = header[5];
                int tagSize = ReadSynchsafeInt(header, 6);
                if (tagSize <= 0 || tagSize > fs.Length - 10)
                    return null;

                byte[] tagData = new byte[tagSize];
                if (fs.Read(tagData, 0, tagData.Length) != tagData.Length)
                    return null;

                if ((flags & 0x80) != 0)
                    tagData = RemoveUnsynchronisation(tagData);

                if (majorVersion == 2)
                    return ReadId3v22ImageBytes(tagData);

                if (majorVersion == 3 || majorVersion == 4)
                    return ReadId3v23Or24ImageBytes(tagData, majorVersion, (flags & 0x40) != 0);
            }

            return null;
        }

        private byte[] ReadId3v23Or24ImageBytes(byte[] tagData, int majorVersion, bool hasExtendedHeader)
        {
            int index = 0;

            if (hasExtendedHeader && tagData.Length >= 4)
            {
                int extendedSize = majorVersion == 4
                    ? ReadSynchsafeInt(tagData, 0)
                    : ReadBigEndianInt(tagData, 0);
                index = Math.Max(0, Math.Min(tagData.Length, extendedSize + (majorVersion == 3 ? 4 : 0)));
            }

            while (index + 10 <= tagData.Length)
            {
                string frameId = System.Text.Encoding.ASCII.GetString(tagData, index, 4);
                if (string.IsNullOrWhiteSpace(frameId) || frameId.Trim('\0').Length == 0)
                    break;

                int frameSize = majorVersion == 4
                    ? ReadSynchsafeInt(tagData, index + 4)
                    : ReadBigEndianInt(tagData, index + 4);
                if (frameSize <= 0 || index + 10 + frameSize > tagData.Length)
                    break;

                if (frameId == "APIC")
                    return ExtractApicImageBytes(tagData, index + 10, frameSize);

                index += 10 + frameSize;
            }

            return null;
        }

        private byte[] ReadId3v22ImageBytes(byte[] tagData)
        {
            int index = 0;
            while (index + 6 <= tagData.Length)
            {
                string frameId = System.Text.Encoding.ASCII.GetString(tagData, index, 3);
                if (string.IsNullOrWhiteSpace(frameId) || frameId.Trim('\0').Length == 0)
                    break;

                int frameSize = (tagData[index + 3] << 16) | (tagData[index + 4] << 8) | tagData[index + 5];
                if (frameSize <= 0 || index + 6 + frameSize > tagData.Length)
                    break;

                if (frameId == "PIC")
                    return ExtractPicImageBytes(tagData, index + 6, frameSize);

                index += 6 + frameSize;
            }

            return null;
        }

        private byte[] ExtractApicImageBytes(byte[] data, int start, int size)
        {
            int end = start + size;
            int index = start;
            if (index >= end)
                return null;

            byte encoding = data[index++];

            while (index < end && data[index] != 0)
                index++;
            index++;

            if (index >= end)
                return null;

            index++;
            index = SkipEncodedTerminatedString(data, index, end, encoding);
            if (index >= end)
                return null;

            return CopyRange(data, index, end - index);
        }

        private byte[] ExtractPicImageBytes(byte[] data, int start, int size)
        {
            int end = start + size;
            int index = start;
            if (index + 5 >= end)
                return null;

            byte encoding = data[index++];
            index += 3;
            index++;
            index = SkipEncodedTerminatedString(data, index, end, encoding);
            if (index >= end)
                return null;

            return CopyRange(data, index, end - index);
        }

        private int SkipEncodedTerminatedString(byte[] data, int index, int end, byte encoding)
        {
            if (encoding == 1 || encoding == 2)
            {
                while (index + 1 < end)
                {
                    if (data[index] == 0 && data[index + 1] == 0)
                        return index + 2;
                    index += 2;
                }
                return end;
            }

            while (index < end && data[index] != 0)
                index++;
            return Math.Min(end, index + 1);
        }

        private int ReadSynchsafeInt(byte[] data, int index)
        {
            return ((data[index] & 0x7F) << 21) |
                   ((data[index + 1] & 0x7F) << 14) |
                   ((data[index + 2] & 0x7F) << 7) |
                   (data[index + 3] & 0x7F);
        }

        private int ReadBigEndianInt(byte[] data, int index)
        {
            return (data[index] << 24) |
                   (data[index + 1] << 16) |
                   (data[index + 2] << 8) |
                   data[index + 3];
        }

        private byte[] RemoveUnsynchronisation(byte[] data)
        {
            var cleaned = new System.Collections.Generic.List<byte>(data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                if (i + 1 < data.Length && data[i] == 0xFF && data[i + 1] == 0x00)
                {
                    cleaned.Add(0xFF);
                    i++;
                }
                else
                {
                    cleaned.Add(data[i]);
                }
            }

            return cleaned.ToArray();
        }

        private byte[] CopyRange(byte[] data, int start, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(data, start, result, 0, length);
            return result;
        }

        private string FindSongSpecificCoverImagePath(Song song)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.FilePath))
                return null;

            string dir = System.IO.Path.GetDirectoryName(song.FilePath);
            if (string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir))
                return null;

            string fileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(song.FilePath);
            string[] exactCandidates = new[]
            {
                $"{fileNameNoExt}.jpg",
                $"{fileNameNoExt}.jpeg",
                $"{fileNameNoExt}.png",
                $"{fileNameNoExt}.bmp",
                $"{fileNameNoExt}.gif"
            };

            foreach (var candidate in exactCandidates)
            {
                string path = System.IO.Path.Combine(dir, candidate);
                if (System.IO.File.Exists(path))
                    return path;
            }

            string normalizedTarget = NormalizeName(fileNameNoExt);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
                return null;

            var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            foreach (var file in System.IO.Directory.EnumerateFiles(dir))
            {
                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (!extensions.Contains(ext))
                    continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                string normalizedName = NormalizeName(name);
                if (normalizedName.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedName))
                    return file;
            }

            return null;
        }

        private void DisposePlaylistImages()
        {
            if (listBoxPlaylist == null || listBoxPlaylist.Rows.Count == 0 || !listBoxPlaylist.Columns.Contains("colImage"))
                return;

            foreach (DataGridViewRow row in listBoxPlaylist.Rows)
            {
                var image = row.Cells["colImage"].Value as Image;
                image?.Dispose();
                row.Cells["colImage"].Value = null;
            }
        }

        private void DisposeImages(System.Collections.Generic.IEnumerable<Image> images)
        {
            foreach (var image in images)
                image?.Dispose();
        }

        private void SetLoadingState(bool isLoading)
        {
            btnLoad.Enabled = !isLoading;
            btnAdd.Enabled = !isLoading;
            this.Cursor = isLoading ? Cursors.WaitCursor : Cursors.Default;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddSongForm frm = new AddSongForm();
            if (frm.ShowDialog() == DialogResult.OK && frm.ResultSong != null)
            {
                if (PlaylistContainsFile(frm.ResultSong.FilePath))
                {
                    MessageBox.Show("File này đã tồn tại trong playlist.", "Trùng lặp",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

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

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (_playlist.Count == 0)
            {
                MessageBox.Show("Playlist đang rỗng!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("Xóa tất cả bài hát trong playlist?", "Xác nhận",
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
            RefreshPlaylistUI();
            lblStatus.Text = "Đã reset playlist";
        }

        private async void btnLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Audio files|*.mp3;*.wav;*.wma;*.aac;*.flac;*.m4a|All files|*.*",
                Multiselect = true,
                Title = "Chọn file nhạc"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            string[] files = dlg.FileNames;
            if (files.Length == 0) return;

            var existingPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in _playlist.ToList())
                existingPaths.Add(s.FilePath);

            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            SetLoadingState(true);
            lblStatus.Text = $"Đang load {files.Length} file...";
            lblLoadTime.Text = "Load: đang chạy...";

            try
            {
                int duplicateCount = 0;
                var songs = await Task.Run(() =>
                {
                    var list = new System.Collections.Generic.List<Song>();
                    var seenFiles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var file in files)
                    {
                        if (!IsValidFile(file))
                            continue;
                        if (existingPaths.Contains(file) || seenFiles.Contains(file))
                        {
                            duplicateCount++;
                            continue;
                        }
                        seenFiles.Add(file);
                        var song = CreateSongFromFile(file);
                        if (song != null)
                            list.Add(song);
                    }
                    return list;
                });

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
                MessageBox.Show("Lỗi khi load file: " + ex.Message, "Lỗi",
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

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (_playlist.Count == 0)
            {
                MessageBox.Show("Playlist đang rỗng!", "Thông báo",
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
                            "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
                        "Lưu playlist", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            catch
            {
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
                catch
                {
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

                    MessageBox.Show("Tên folder không được để trống.", "Thông báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return false;
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            TogglePlayPause();
        }

        private void TogglePlayPause()
        {
            if (_wmp == null)
                return;

            if (_wmp.playState == WMPPlayState.wmppsPlaying)
            {
                _wmp.controls.pause();
                SetPlayButtonState(false);
                return;
            }

            if (_wmp.playState == WMPPlayState.wmppsPaused)
            {
                _wmp.controls.play();
                SetPlayButtonState(true);
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
            if (!Shuffle.Checked)
            {
                lblStatus.Text = "Shuffle tắt";
                return;
            }

            if (_playlist == null || _playlist.Count <= 1)
            {
                lblStatus.Text = "Playlist không đủ bài để xáo";
                return;
            }

            _playlist.Shuffle();
            RefreshPlaylistUI();
            lblStatus.Text = "Đã xáo trộn playlist";
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
            DisposePlaylistImages();
            listBoxPlaylist.Rows.Clear();
            for (int i = 0; i < kq.Count; i++)
            {
                var rowIndex = listBoxPlaylist.Rows.Add(i + 1, kq[i].Title, kq[i].Artist, "--:--", null);
                listBoxPlaylist.Rows[rowIndex].Tag = kq[i].ID;
            }
            UpdatePlaylistDurationsAsync(kq);
            UpdatePlaylistImagesAsync(kq);
        }

        private void chkRepeatAll_CheckedChanged(object sender, EventArgs e)
        {
            _playlist.IsRepeatAll = chkRepeatAll.Checked;
        }
    }
}
