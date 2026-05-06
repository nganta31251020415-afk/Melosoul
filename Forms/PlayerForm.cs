using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Melosoul.Helpers;
using Melosoul.Services;
using WMPLib;
using System.Runtime.InteropServices;
namespace Melosoul
{
    public partial class PlayerForm : Form
    {
        private static readonly string[] SupportedExtensions =
            { ".mp3", ".mp4", ".wav", ".wma", ".aac", ".flac", ".m4a" };

        private PlaylistLinkedList _playlist = new PlaylistLinkedList();
        private WindowsMediaPlayer _wmp;
        private Timer _playbackTimer;
        private Timer _playlistClickTimer;
        private DataGridViewCellMouseEventArgs _pendingPlaylistClick;
        private CustomPlaylistScrollBar _playlistScrollBar;
        private bool _syncingPlaylistScrollBar;
        private readonly AlbumArtService _albumArtService = new AlbumArtService();
        private readonly AutoSaveService _autoSaveService = new AutoSaveService();
        private int _autoNextQueued;
        private string _currentAlbumSongId;

        [System.Runtime.InteropServices.DllImport("uxtheme.dll",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string app, string id);

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr,
            ref int attrValue, int attrSize);

        public PlayerForm()
        {
            InitializeComponent();
            InitWMP();
            InitializePlaybackTimer();
            InitializePlaylistClickTimer();
            InitializePlaylistDragDrop();
            InitializePlaylistScrollBar();
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

        private void InitializePlaylistDragDrop()
        {
            dgvPlaylist.AllowDrop = true;
            dgvPlaylist.DragEnter += dgvPlaylist_DragEnter;
            dgvPlaylist.DragOver += dgvPlaylist_DragEnter;
            dgvPlaylist.DragDrop += dgvPlaylist_DragDrop;
        }

        private void InitializePlaylistScrollBar()
        {
            _playlistScrollBar = new CustomPlaylistScrollBar();
            _playlistScrollBar.ValueChanged += PlaylistScrollBar_ValueChanged;
            Controls.Add(_playlistScrollBar);
            _playlistScrollBar.BringToFront();

            dgvPlaylist.Scroll += dgvPlaylist_Scroll;
            dgvPlaylist.MouseWheel += dgvPlaylist_MouseWheel;
            dgvPlaylist.RowsAdded += dgvPlaylist_RowsChanged;
            dgvPlaylist.RowsRemoved += dgvPlaylist_RowsChanged;
            dgvPlaylist.SizeChanged += dgvPlaylist_SizeChanged;
            dgvPlaylist.ColumnWidthChanged += dgvPlaylist_ColumnWidthChanged;
            Resize += PlayerForm_Resize;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                duration = 0;
                position = 0;
            }

            lblTimeStart.Text = FormatTime(position);
            lblTimeEnd.Text = duration > 0 ? FormatTime(duration) : "--:--";

            if (duration > 0)
            {
                int percent = (int)Math.Round((position / duration) * 100);
                progressBar.Value = Math.Min(100, Math.Max(0, percent));

                if (position >= duration - 0.1)
                    QueueAutoNext(300);
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void WMP_PlayStateChange(int NewState)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (NewState == (int)WMPPlayState.wmppsMediaEnded)
            {
                QueueAutoNext(300);
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

        private void QueueAutoNext(int delayMs)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (System.Threading.Interlocked.Exchange(ref _autoNextQueued, 1) == 1)
                return;

            BeginInvoke(new Action(() =>
            {
                Task.Delay(delayMs).ContinueWith(_ =>
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(AutoNext));
                });
            }));
        }

        private void AutoNext()
        {
            System.Threading.Interlocked.Exchange(ref _autoNextQueued, 0);

            if (chkRepeatOne.Checked && _playlist.CurrentSong != null)
            {
                Song currentSong = _playlist.CurrentSong;
                if (IsValidFile(currentSong)) { PlaySong(currentSong); RefreshPlaylistUI(); return; }
            }

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
                System.Threading.Interlocked.Exchange(ref _autoNextQueued, 0);
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
                System.Threading.Interlocked.Exchange(ref _autoNextQueued, 0);
                _wmp.controls.stop();
                _wmp.URL = "";
                SetPlayButtonState(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private string GetDuration(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return "--:--";
                using (var f = TagLib.File.Create(filePath))
                {
                    double dur = f.Properties.Duration.TotalSeconds;
                    if (dur <= 0) return "--:--";
                    int min = (int)(dur / 60);
                    int sec = (int)(dur % 60);
                    return $"{min}:{sec:D2}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return "--:--";
            }
        }

        private Image _albumCover;

        private bool IsValidFile(Song song) =>
            song != null && IsValidFile(song.FilePath);

        private bool IsValidFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            if (!System.IO.File.Exists(filePath)) return false;
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            return Array.Exists(SupportedExtensions, e => e == ext);
        }

        private bool PlaylistContainsFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            return _playlist.ToList().Any(s =>
                string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        private Song FindSongById(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId))
                return null;

            return _playlist.ToList().Find(s => s.ID == songId);
        }

        private Song GetSongFromRow(DataGridViewRow row)
        {
            return FindSongById(row?.Tag?.ToString());
        }

        private Song CreateSongFromFile(string filePath)
        {
            WindowsMediaPlayer player = null;
            IWMPMedia media = null;
            try
            {
                string title = null;
                string artist = null;
                player = new WindowsMediaPlayer();
                media = player.newMedia(filePath);
                title = media.getItemInfo("Title");
                artist = media.getItemInfo("Author");
                if (string.IsNullOrWhiteSpace(artist))
                    artist = media.getItemInfo("WM/AlbumArtist");
                if (string.IsNullOrWhiteSpace(artist))
                    artist = media.getItemInfo("WM/Artist");
                if (string.IsNullOrWhiteSpace(title))
                    title = System.IO.Path.GetFileNameWithoutExtension(filePath);

                return new Song(Guid.NewGuid().ToString(), title.Trim(), artist?.Trim() ?? "", filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                string title = System.IO.Path.GetFileNameWithoutExtension(filePath);
                return new Song(Guid.NewGuid().ToString(), title, string.Empty, filePath);
            }
            finally
            {
                if (media != null)
                    Marshal.ReleaseComObject(media);
                if (player != null)
                    Marshal.ReleaseComObject(player);
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

        private void UpdateAlbumCover(Song song)
        {
            if (picAlbum == null)
                return;

            DisposeAlbumCover();
            picAlbum.Image = null;

            if (song == null || string.IsNullOrWhiteSpace(song.FilePath))
                return;

            _currentAlbumSongId = song.ID;
            int coverSize = Math.Max(picAlbum.Width, picAlbum.Height);

            Task.Run(() =>
            {
                try
                {
                    if (song.ID != _currentAlbumSongId)
                        return;

                    Bitmap loadedImage = _albumArtService.CreateAlbumCoverImage(song, coverSize);
                    if (loadedImage == null)
                        loadedImage = _albumArtService.CreateDefaultMusicNoteImage(coverSize);

                    if (song.ID != _currentAlbumSongId || IsDisposed || picAlbum.IsDisposed)
                    {
                        loadedImage.Dispose();
                        return;
                    }

                    if (picAlbum.InvokeRequired)
                    {
                        picAlbum.BeginInvoke(new Action(() =>
                        {
                            if (!IsDisposed && !picAlbum.IsDisposed && song.ID == _currentAlbumSongId)
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                }
            });
        }

        private void PlayerForm_Load(object sender, EventArgs e)
        {
            int color = unchecked((int)0xFFC8AFFF);
            DwmSetWindowAttribute(this.Handle, 35, ref color, sizeof(int));

            SetWindowTheme(progressBar.Handle, "", "");

            dgvPlaylist.EnableHeadersVisualStyles = false;
            dgvPlaylist.AllowUserToAddRows = false;
            dgvPlaylist.RowHeadersVisible = false;
            dgvPlaylist.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvPlaylist.GridColor = Color.FromArgb(45, 25, 38);
            dgvPlaylist.BackgroundColor = Color.FromArgb(18, 18, 18);
            dgvPlaylist.BorderStyle = BorderStyle.None;
            dgvPlaylist.MultiSelect = false;
            dgvPlaylist.ScrollBars = ScrollBars.None;
            dgvPlaylist.ClearSelection();

            dgvPlaylist.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 15, 28);
            dgvPlaylist.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(200, 130, 160);
            dgvPlaylist.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            dgvPlaylist.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            dgvPlaylist.DefaultCellStyle.BackColor = Color.FromArgb(18, 18, 18);
            dgvPlaylist.DefaultCellStyle.ForeColor = Color.FromArgb(204, 204, 204);
            dgvPlaylist.DefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 30, 60);
            dgvPlaylist.DefaultCellStyle.SelectionForeColor = Color.FromArgb(255, 255, 255);
            dgvPlaylist.DefaultCellStyle.Font = new Font("Segoe UI", 9f);

            dgvPlaylist.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(22, 22, 22);
            dgvPlaylist.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(204, 204, 204);
            dgvPlaylist.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 30, 60);
            dgvPlaylist.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(255, 255, 255);

            colNum.SortMode = DataGridViewColumnSortMode.NotSortable;
            colTitle.SortMode = DataGridViewColumnSortMode.NotSortable;
            colArtist.SortMode = DataGridViewColumnSortMode.NotSortable;
            colDuration.SortMode = DataGridViewColumnSortMode.NotSortable;
            colImage.SortMode = DataGridViewColumnSortMode.NotSortable;
            colImage.Visible = false;
            ConfigureStatusSeparators();

            foreach (var song in _autoSaveService.Load())
                _playlist.AddLast(song);

            RefreshPlaylistUI();
        }

        private void ConfigureStatusSeparators()
        {
            lblSongCount.BorderSides = ToolStripStatusLabelBorderSides.None;
            lblLoadTime.BorderSides = ToolStripStatusLabelBorderSides.None;

            lblSongCount.Paint -= StatusSeparator_Paint;
            lblLoadTime.Paint -= StatusSeparator_Paint;
            lblSongCount.Paint += StatusSeparator_Paint;
            lblLoadTime.Paint += StatusSeparator_Paint;
        }

        private void StatusSeparator_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Color.Black))
            {
                e.Graphics.DrawLine(pen, 0, 0, 0, e.ClipRectangle.Height);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _autoSaveService.Save(_playlist);
            UnwirePlaylistScrollBar();
            _playbackTimer?.Stop();
            _playlistClickTimer?.Stop();
            if (_wmp != null)
            {
                try
                {
                    _wmp.PlayStateChange -= WMP_PlayStateChange;
                    _wmp.MediaError -= WMP_MediaError;
                    _wmp.controls.stop();
                    Marshal.ReleaseComObject(_wmp);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                }
                finally { _wmp = null; }
            }
            _playlist.Dispose();
            DisposePlaylistImages();
            DisposeAlbumCover();
            _playbackTimer?.Dispose();
            _playlistClickTimer?.Dispose();
            _playlistScrollBar?.Dispose();
            base.OnFormClosing(e);
        }

        private void UnwirePlaylistScrollBar()
        {
            if (_playlistScrollBar != null)
                _playlistScrollBar.ValueChanged -= PlaylistScrollBar_ValueChanged;

            if (dgvPlaylist != null)
            {
                dgvPlaylist.Scroll -= dgvPlaylist_Scroll;
                dgvPlaylist.MouseWheel -= dgvPlaylist_MouseWheel;
                dgvPlaylist.RowsAdded -= dgvPlaylist_RowsChanged;
                dgvPlaylist.RowsRemoved -= dgvPlaylist_RowsChanged;
                dgvPlaylist.SizeChanged -= dgvPlaylist_SizeChanged;
                dgvPlaylist.ColumnWidthChanged -= dgvPlaylist_ColumnWidthChanged;
            }

            Resize -= PlayerForm_Resize;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (ActiveControl is TextBox)
                return base.ProcessCmdKey(ref msg, keyData);

            if (keyData == Keys.Space)
            {
                TogglePlayPause();
                return true;
            }

            if (keyData == Keys.Right || keyData == (Keys.Control | Keys.Right))
            {
                btnNext_Click(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.Left || keyData == (Keys.Control | Keys.Left))
            {
                btnPrev_Click(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshPlaylistUI()
        {
            var list = _playlist.ToList();
            DisposePlaylistImages();
            dgvPlaylist.Rows.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var rowIndex = dgvPlaylist.Rows.Add(i + 1, list[i].Title, list[i].Artist, "--:--", null);
                dgvPlaylist.Rows[rowIndex].Tag = list[i].ID;
            }
            lblSongCount.Text = $"{_playlist.Count} bài hát";
            UpdatePlaylistScrollBar();
            UpdatePlaylistDurationsAsync(list);
            UpdatePlaylistImagesAsync(list);
            UpdatePlaylistSelection();
        }

        private void UpdatePlaylistSelection()
        {
            if (dgvPlaylist.IsDisposed) return;

            if (dgvPlaylist.InvokeRequired)
            {
                dgvPlaylist.Invoke(new Action(UpdatePlaylistSelection));
                return;
            }

            dgvPlaylist.ClearSelection();
            dgvPlaylist.CurrentCell = null;

            var current = _playlist.CurrentSong;
            if (current == null)
            {
                UpdatePlaylistScrollBar();
                return;
            }

            var list = _playlist.ToList();
            int index = list.FindIndex(s => s.ID == current.ID);
            if (index < 0 || index >= dgvPlaylist.Rows.Count)
            {
                UpdatePlaylistScrollBar();
                return;
            }

            var row = dgvPlaylist.Rows[index];
            row.Selected = true;
            if (dgvPlaylist.Columns.Contains("colTitle"))
            {
                dgvPlaylist.CurrentCell = row.Cells["colTitle"];
            }
            else if (dgvPlaylist.Columns.Count > 0)
            {
                dgvPlaylist.CurrentCell = row.Cells[0];
            }

            try
            {
                dgvPlaylist.FirstDisplayedScrollingRowIndex = index;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }

            UpdatePlaylistScrollBar();
        }

        private void PlaylistScrollBar_ValueChanged(object sender, EventArgs e)
        {
            if (_syncingPlaylistScrollBar || dgvPlaylist.Rows.Count == 0)
                return;

            int targetIndex = Math.Max(0, Math.Min(_playlistScrollBar.Value, dgvPlaylist.Rows.Count - 1));
            try
            {
                _syncingPlaylistScrollBar = true;
                dgvPlaylist.FirstDisplayedScrollingRowIndex = targetIndex;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _syncingPlaylistScrollBar = false;
            }

            UpdatePlaylistScrollBar();
        }

        private void dgvPlaylist_Scroll(object sender, ScrollEventArgs e)
        {
            UpdatePlaylistScrollBar();
        }

        private void dgvPlaylist_MouseWheel(object sender, MouseEventArgs e)
        {
            ScrollPlaylistByRows(-Math.Sign(e.Delta) * 3);
        }

        private void dgvPlaylist_RowsChanged(object sender, EventArgs e)
        {
            UpdatePlaylistScrollBar();
        }

        private void dgvPlaylist_SizeChanged(object sender, EventArgs e)
        {
            UpdatePlaylistScrollBar();
        }

        private void dgvPlaylist_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            UpdatePlaylistScrollBar();
        }

        private void PlayerForm_Resize(object sender, EventArgs e)
        {
            UpdatePlaylistScrollBar();
        }

        private void ScrollPlaylistByRows(int rowDelta)
        {
            if (dgvPlaylist.Rows.Count == 0)
                return;

            int firstRowIndex = GetFirstDisplayedPlaylistRowIndex();
            int visibleRows = GetVisiblePlaylistRowCount();
            int maxFirstRowIndex = Math.Max(0, dgvPlaylist.Rows.Count - visibleRows);
            int targetIndex = Math.Max(0, Math.Min(maxFirstRowIndex, firstRowIndex + rowDelta));

            try
            {
                dgvPlaylist.FirstDisplayedScrollingRowIndex = targetIndex;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }

            UpdatePlaylistScrollBar();
        }

        private void UpdatePlaylistScrollBar()
        {
            if (_playlistScrollBar == null || dgvPlaylist == null || dgvPlaylist.IsDisposed)
                return;

            int visibleRows = GetVisiblePlaylistRowCount();
            int maxFirstRowIndex = Math.Max(0, dgvPlaylist.Rows.Count - visibleRows);
            int firstRowIndex = Math.Min(GetFirstDisplayedPlaylistRowIndex(), maxFirstRowIndex);
            bool shouldShow = dgvPlaylist.Rows.Count > visibleRows;

            _syncingPlaylistScrollBar = true;
            _playlistScrollBar.SetScrollInfo(0, maxFirstRowIndex, firstRowIndex, visibleRows);
            _playlistScrollBar.Visible = shouldShow;
            _syncingPlaylistScrollBar = false;

            UpdatePlaylistScrollBarBounds();
        }

        private void UpdatePlaylistScrollBarBounds()
        {
            if (_playlistScrollBar == null)
                return;

            const int width = 12;
            const int marginRight = 4;
            int top = dgvPlaylist.Top + dgvPlaylist.ColumnHeadersHeight + 4;
            int bottom = dgvPlaylist.Bottom - 4;
            _playlistScrollBar.Bounds = new Rectangle(
                dgvPlaylist.Right - width - marginRight,
                top,
                width,
                Math.Max(1, bottom - top));
            _playlistScrollBar.BringToFront();
        }

        private int GetVisiblePlaylistRowCount()
        {
            if (dgvPlaylist.Rows.Count == 0)
                return 1;

            try
            {
                int displayed = dgvPlaylist.DisplayedRowCount(false);
                if (displayed > 0)
                    return displayed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }

            return Math.Max(1, (dgvPlaylist.ClientSize.Height - dgvPlaylist.ColumnHeadersHeight) / Math.Max(1, dgvPlaylist.RowTemplate.Height));
        }

        private int GetFirstDisplayedPlaylistRowIndex()
        {
            try
            {
                return dgvPlaylist.Rows.Count == 0 ? 0 : dgvPlaylist.FirstDisplayedScrollingRowIndex;
            }
            catch
            {
                return 0;
            }
        }

        private void InitializePlaylistClickTimer()
        {
            _playlistClickTimer = new Timer();
            _playlistClickTimer.Interval = Math.Min(SystemInformation.DoubleClickTime, 80);
            _playlistClickTimer.Tick += (sender, e) =>
            {
                _playlistClickTimer.Stop();
                if (_pendingPlaylistClick == null) return;
                HandlePlaylistSingleClick(_pendingPlaylistClick);
                _pendingPlaylistClick = null;
            };
        }

        private void dgvPlaylist_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.Button != MouseButtons.Left) return;
            _pendingPlaylistClick = e;
            _playlistClickTimer.Stop();
            _playlistClickTimer.Start();
        }

        private void dgvPlaylist_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            _playlistClickTimer.Stop();
            _pendingPlaylistClick = null;

            if (e.RowIndex < 0) return;
            if (dgvPlaylist.Rows.Count <= e.RowIndex) return;

            var row = dgvPlaylist.Rows[e.RowIndex];
            var song = GetSongFromRow(row);
            if (song == null) return;

            var oldTitle = song.Title;
            var oldArtist = song.Artist;
            using (var dialog = new RenameSongDialog(oldTitle, oldArtist))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string newTitle = dialog.SongTitle;
                    string newArtist = dialog.SongArtist;
                    bool titleChanged = !string.IsNullOrWhiteSpace(newTitle) && newTitle != oldTitle;
                    bool artistChanged = newArtist != oldArtist;
                    if (titleChanged || artistChanged)
                    {
                        if (!_playlist.UpdateSongMetadata(song.ID, newTitle, newArtist))
                            return;

                        row.Cells["colTitle"].Value = newTitle;
                        row.Cells["colArtist"].Value = newArtist;

                        if (_playlist.CurrentSong != null && _playlist.CurrentSong.ID == song.ID)
                        {
                            lblCurrentSong.Text = newTitle;
                            lblArtist.Text = newArtist;
                        }
                    }
                }
            }
        }

        private void HandlePlaylistSingleClick(DataGridViewCellMouseEventArgs e)
        {
            if (dgvPlaylist.IsDisposed) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (e.RowIndex >= dgvPlaylist.Rows.Count) return;
            var row = dgvPlaylist.Rows[e.RowIndex];
            var song = GetSongFromRow(row);
            if (song == null) return;

            _playlist.MoveTo(song.ID);
            PlaySong(song);
            UpdatePlaylistSelection();
        }

        private void dgvPlaylist_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // Removed: using popup dialog instead of inline edit
        }

        private void dgvPlaylist_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
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

                if (dgvPlaylist.IsDisposed) return;

                if (dgvPlaylist.InvokeRequired)
                {
                    dgvPlaylist.Invoke(new Action(() =>
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
            for (int i = 0; i < dgvPlaylist.Rows.Count; i++)
            {
                var row = dgvPlaylist.Rows[i];
                var song = GetSongFromRow(row);
                if (song != null && durationMap.TryGetValue(song.ID, out string duration))
                {
                    row.Cells["colDuration"].Value = duration;
                }
            }
        }

        private void UpdatePlaylistImagesAsync(System.Collections.Generic.List<Song> list)
        {
            if (dgvPlaylist == null ||
                !dgvPlaylist.Columns.Contains("colImage") ||
                !dgvPlaylist.Columns["colImage"].Visible)
            {
                return;
            }

            Task.Run(() =>
            {
                var imageMap = new System.Collections.Generic.Dictionary<string, Image>();
                foreach (var song in list)
                {
                    var image = _albumArtService.CreatePlaylistThumbnail(song);
                    if (image != null)
                        imageMap[song.ID] = image;
                }

                if (dgvPlaylist.IsDisposed)
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                        DisposeImages(imageMap.Values);
                    }
                };

                if (dgvPlaylist.InvokeRequired)
                    dgvPlaylist.Invoke(updateAction);
                else
                    updateAction();
            });
        }

        private void UpdateImagesInUI(System.Collections.Generic.Dictionary<string, Image> imageMap)
        {
            var usedImages = new System.Collections.Generic.HashSet<Image>();
            for (int i = 0; i < dgvPlaylist.Rows.Count; i++)
            {
                var row = dgvPlaylist.Rows[i];
                string songId = row.Tag?.ToString();
                if (string.IsNullOrEmpty(songId)) continue;

                if (!imageMap.TryGetValue(songId, out Image image))
                    continue;

                if (!ReferenceEquals(row.Cells["colImage"].Value, image))
                {
                    var oldImage = row.Cells["colImage"].Value as Image;
                    row.Cells["colImage"].Value = image;
                    oldImage?.Dispose();
                }

                usedImages.Add(image);
            }

            foreach (var pair in imageMap)
            {
                if (!usedImages.Contains(pair.Value))
                    pair.Value.Dispose();
            }
        }

        private void DisposePlaylistImages()
        {
            if (dgvPlaylist == null || dgvPlaylist.Rows.Count == 0 || !dgvPlaylist.Columns.Contains("colImage"))
                return;

            foreach (DataGridViewRow row in dgvPlaylist.Rows)
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
            using (AddSongDialog frm = new AddSongDialog())
            {
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
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (dgvPlaylist.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn bài muốn xóa!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var song = GetSongFromRow(dgvPlaylist.SelectedRows[0]);
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
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Audio files|*.mp3;*.wav;*.wma;*.aac;*.flac;*.m4a|All files|*.*",
                Multiselect = true,
                Title = "Chọn file nhạc"
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
            dgvPlaylist.Rows.Clear();
            for (int i = 0; i < kq.Count; i++)
            {
                var rowIndex = dgvPlaylist.Rows.Add(i + 1, kq[i].Title, kq[i].Artist, "--:--", null);
                dgvPlaylist.Rows[rowIndex].Tag = kq[i].ID;
            }
            lblSongCount.Text = $"{kq.Count}/{_playlist.Count} bài hát";
            UpdatePlaylistScrollBar();
            UpdatePlaylistDurationsAsync(kq);
            UpdatePlaylistImagesAsync(kq);
        }

        private void chkRepeatAll_CheckedChanged(object sender, EventArgs e)
        {
            _playlist.IsRepeatAll = chkRepeatAll.Checked;
        }
    }
}


