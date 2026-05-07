using System;
using System.Drawing;
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
        private bool _isSeekingProgressBar;
        private readonly AlbumArtService _albumArtService = new AlbumArtService();
        private readonly AutoSaveService _autoSaveService = new AutoSaveService();
        private readonly MediaMetadataService _metadataService = new MediaMetadataService();
        private readonly DurationService _durationService = new DurationService();
        private readonly FileImportService _fileImportService;
        private int _autoNextQueued;
        private int _playlistRenderVersion;
        private string _currentAlbumSongId;
        private System.Threading.CancellationTokenSource _durationUpdateCts;
        private Image _albumCover;

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string app, string id);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public PlayerForm()
        {
            InitializeComponent();
            _fileImportService = new FileImportService(new PlaylistLoaderService(_metadataService));
            InitWMP();
            InitializePlaybackTimer();
            InitializePlaylistClickTimer();
            InitializePlaylistDragDrop();
            InitializePlaylistScrollBar();
            InitializeProgressBarSeeking();
        }

        private string GetDuration(string filePath)
        {
            return _durationService.Resolve(filePath);
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
            if (picAlbum == null) return;
            DisposeAlbumCover();
            picAlbum.Image = null;
            if (song == null || string.IsNullOrWhiteSpace(song.FilePath)) return;

            _currentAlbumSongId = song.ID;
            int coverSize = Math.Max(picAlbum.Width, picAlbum.Height);

            Task.Run(() =>
            {
                try
                {
                    if (song.ID != _currentAlbumSongId) return;
                    Bitmap loadedImage = _albumArtService.CreateAlbumCoverImage(song, coverSize) ??
                                         _albumArtService.CreateDefaultMusicNoteImage(coverSize);
                    if (song.ID != _currentAlbumSongId || IsDisposed || picAlbum.IsDisposed) { loadedImage.Dispose(); return; }
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
                            else loadedImage.Dispose();
                        }));
                    }
                    else
                    {
                        DisposeAlbumCover();
                        _albumCover = loadedImage;
                        picAlbum.Image = _albumCover;
                    }
                }
                catch { }
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
            dgvPlaylist.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 15, 28);
            dgvPlaylist.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(200, 130, 160);
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
            dgvPlaylist.ColumnHeaderMouseClick += dgvPlaylist_ColumnHeaderMouseClick;

            ConfigureStatusSeparators();
            foreach (var song in _autoSaveService.Load()) _playlist.AddLast(song);
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
                e.Graphics.DrawLine(pen, 0, 0, 0, e.ClipRectangle.Height);
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
                catch { }
                finally { _wmp = null; }
            }
            _playlist.Dispose();
            DisposePlaylistImages();
            DisposeAlbumCover();
            _playbackTimer?.Dispose();
            _playlistClickTimer?.Dispose();
            _playlistScrollBar?.Dispose();
            _durationUpdateCts?.Cancel();
            _durationUpdateCts?.Dispose();
            base.OnFormClosing(e);
        }

        private void UnwirePlaylistScrollBar()
        {
            if (_playlistScrollBar != null) _playlistScrollBar.ValueChanged -= PlaylistScrollBar_ValueChanged;
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
            if (ActiveControl is TextBox) return base.ProcessCmdKey(ref msg, keyData);
            if (keyData == Keys.Space) { TogglePlayPause(); return true; }
            if (keyData == Keys.Right || keyData == (Keys.Control | Keys.Right)) { btnNext_Click(this, EventArgs.Empty); return true; }
            if (keyData == Keys.Left || keyData == (Keys.Control | Keys.Left)) { btnPrev_Click(this, EventArgs.Empty); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
