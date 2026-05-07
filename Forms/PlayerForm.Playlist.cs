using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Melosoul.Helpers;
using Melosoul.Services;

namespace Melosoul
{
    public partial class PlayerForm
    {
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

        private bool IsValidFile(Song song) => song != null && IsValidFile(song.FilePath);

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
            return _playlist.ToList().Any(s => string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        private Song FindSongById(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId)) return null;
            return _playlist.GetById(songId);
        }

        private Song GetSongFromRow(DataGridViewRow row)
        {
            return FindSongById(row?.Tag?.ToString());
        }

        private void RenderPlaylist(System.Collections.Generic.List<Song> list, string countText)
        {
            if (list == null) list = new System.Collections.Generic.List<Song>();
            int renderVersion = System.Threading.Interlocked.Increment(ref _playlistRenderVersion);
            DisposePlaylistImages();
            dgvPlaylist.Rows.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var rowIndex = dgvPlaylist.Rows.Add(i + 1, list[i].Title, list[i].Artist, AppText.UnknownDuration, null);
                dgvPlaylist.Rows[rowIndex].Tag = list[i].ID;
            }

            lblSongCount.Text = countText;
            UpdatePlaylistScrollBar();
            UpdatePlaylistDurationsAsync(list, renderVersion);
            UpdatePlaylistImagesAsync(list, renderVersion);
            UpdatePlaylistSelection();
        }

        private void RefreshPlaylistUI()
        {
            var list = _playlist.ToList();
            RenderPlaylist(list, $"{list.Count} bài hát");
        }

        private void UpdatePlaylistSelection()
        {
            if (dgvPlaylist.IsDisposed) return;
            if (dgvPlaylist.InvokeRequired) { dgvPlaylist.Invoke(new Action(UpdatePlaylistSelection)); return; }

            dgvPlaylist.ClearSelection();
            dgvPlaylist.CurrentCell = null;
            var current = _playlist.CurrentSong;
            if (current == null) { UpdatePlaylistScrollBar(); return; }

            var list = _playlist.ToList();
            int index = list.FindIndex(s => s.ID == current.ID);
            if (index < 0 || index >= dgvPlaylist.Rows.Count) { UpdatePlaylistScrollBar(); return; }

            var row = dgvPlaylist.Rows[index];
            row.Selected = true;
            dgvPlaylist.CurrentCell = dgvPlaylist.Columns.Contains("colTitle") ? row.Cells["colTitle"] : row.Cells[0];
            try { dgvPlaylist.FirstDisplayedScrollingRowIndex = index; } catch { }
            UpdatePlaylistScrollBar();
        }

        private void PlaylistScrollBar_ValueChanged(object sender, EventArgs e)
        {
            if (_syncingPlaylistScrollBar || dgvPlaylist.Rows.Count == 0) return;
            int targetIndex = Math.Max(0, Math.Min(_playlistScrollBar.Value, dgvPlaylist.Rows.Count - 1));
            try
            {
                _syncingPlaylistScrollBar = true;
                dgvPlaylist.FirstDisplayedScrollingRowIndex = targetIndex;
            }
            catch { }
            finally { _syncingPlaylistScrollBar = false; }
            UpdatePlaylistScrollBar();
        }

        private void dgvPlaylist_Scroll(object sender, ScrollEventArgs e) { UpdatePlaylistScrollBar(); }
        private void dgvPlaylist_MouseWheel(object sender, MouseEventArgs e) { ScrollPlaylistByRows(-Math.Sign(e.Delta) * 3); }
        private void dgvPlaylist_RowsChanged(object sender, EventArgs e) { UpdatePlaylistScrollBar(); }
        private void dgvPlaylist_SizeChanged(object sender, EventArgs e) { UpdatePlaylistScrollBar(); }
        private void dgvPlaylist_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e) { UpdatePlaylistScrollBar(); }
        private void PlayerForm_Resize(object sender, EventArgs e) { UpdatePlaylistScrollBar(); }

        private void ScrollPlaylistByRows(int rowDelta)
        {
            if (dgvPlaylist.Rows.Count == 0) return;
            int firstRowIndex = GetFirstDisplayedPlaylistRowIndex();
            int visibleRows = GetVisiblePlaylistRowCount();
            int maxFirstRowIndex = Math.Max(0, dgvPlaylist.Rows.Count - visibleRows);
            int targetIndex = Math.Max(0, Math.Min(maxFirstRowIndex, firstRowIndex + rowDelta));
            try { dgvPlaylist.FirstDisplayedScrollingRowIndex = targetIndex; } catch { }
            UpdatePlaylistScrollBar();
        }

        private void UpdatePlaylistScrollBar()
        {
            if (_playlistScrollBar == null || dgvPlaylist == null || dgvPlaylist.IsDisposed) return;
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
            if (_playlistScrollBar == null) return;
            const int width = 12;
            const int marginRight = 4;
            int top = dgvPlaylist.Top + dgvPlaylist.ColumnHeadersHeight + 4;
            int bottom = dgvPlaylist.Bottom - 4;
            _playlistScrollBar.Bounds = new Rectangle(dgvPlaylist.Right - width - marginRight, top, width, Math.Max(1, bottom - top));
            _playlistScrollBar.BringToFront();
        }

        private int GetVisiblePlaylistRowCount()
        {
            if (dgvPlaylist.Rows.Count == 0) return 1;
            try { int displayed = dgvPlaylist.DisplayedRowCount(false); if (displayed > 0) return displayed; } catch { }
            return Math.Max(1, (dgvPlaylist.ClientSize.Height - dgvPlaylist.ColumnHeadersHeight) / Math.Max(1, dgvPlaylist.RowTemplate.Height));
        }

        private int GetFirstDisplayedPlaylistRowIndex()
        {
            try { return dgvPlaylist.Rows.Count == 0 ? 0 : dgvPlaylist.FirstDisplayedScrollingRowIndex; } catch { return 0; }
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
                        if (!_playlist.UpdateSongMetadata(song.ID, newTitle, newArtist)) return;
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

        private void dgvPlaylist_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            dgvPlaylist.ClearSelection();
            dgvPlaylist.CurrentCell = null;
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

        private void dgvPlaylist_CellEndEdit(object sender, DataGridViewCellEventArgs e) { }
        private void dgvPlaylist_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e) { }

        private void UpdatePlaylistDurationsAsync(System.Collections.Generic.List<Song> list, int renderVersion)
        {
            _durationUpdateCts?.Cancel();
            _durationUpdateCts?.Dispose();
            _durationUpdateCts = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = _durationUpdateCts.Token;

            Task.Run(() =>
            {
                var durationMap = new System.Collections.Generic.Dictionary<string, string>();
                foreach (var song in list)
                {
                    if (token.IsCancellationRequested) return;
                    string duration = GetDuration(song.FilePath);
                    durationMap[song.ID] = duration;
                }

                if (renderVersion != _playlistRenderVersion) return;
                if (dgvPlaylist.IsDisposed) return;
                if (dgvPlaylist.InvokeRequired)
                {
                    dgvPlaylist.Invoke(new Action(() =>
                    {
                        if (renderVersion == _playlistRenderVersion) UpdateDurationsInUI(durationMap);
                    }));
                }
                else
                {
                    if (renderVersion == _playlistRenderVersion) UpdateDurationsInUI(durationMap);
                }
            }, token);
        }

        private void UpdateDurationsInUI(System.Collections.Generic.Dictionary<string, string> durationMap)
        {
            for (int i = 0; i < dgvPlaylist.Rows.Count; i++)
            {
                var row = dgvPlaylist.Rows[i];
                string songId = row.Tag?.ToString();
                if (!string.IsNullOrEmpty(songId) && durationMap.TryGetValue(songId, out string duration))
                    row.Cells["colDuration"].Value = duration;
            }
        }

        private void UpdatePlaylistImagesAsync(System.Collections.Generic.List<Song> list, int renderVersion)
        {
            if (dgvPlaylist == null || !dgvPlaylist.Columns.Contains("colImage") || !dgvPlaylist.Columns["colImage"].Visible) return;
            Task.Run(() =>
            {
                var imageMap = new System.Collections.Generic.Dictionary<string, Image>();
                foreach (var song in list)
                {
                    var image = _albumArtService.CreatePlaylistThumbnail(song);
                    if (image != null) imageMap[song.ID] = image;
                }
                if (renderVersion != _playlistRenderVersion) { DisposeImages(imageMap.Values); return; }
                if (dgvPlaylist.IsDisposed) { DisposeImages(imageMap.Values); return; }
                Action updateAction = () =>
                {
                    try { UpdateImagesInUI(imageMap); }
                    catch { DisposeImages(imageMap.Values); }
                };
                if (dgvPlaylist.InvokeRequired) dgvPlaylist.Invoke(updateAction); else updateAction();
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
                if (!imageMap.TryGetValue(songId, out Image image)) continue;
                if (!ReferenceEquals(row.Cells["colImage"].Value, image))
                {
                    var oldImage = row.Cells["colImage"].Value as Image;
                    row.Cells["colImage"].Value = image;
                    oldImage?.Dispose();
                }
                usedImages.Add(image);
            }
            foreach (var pair in imageMap) if (!usedImages.Contains(pair.Value)) pair.Value.Dispose();
        }

        private void DisposePlaylistImages()
        {
            if (dgvPlaylist == null || dgvPlaylist.Rows.Count == 0 || !dgvPlaylist.Columns.Contains("colImage")) return;
            foreach (DataGridViewRow row in dgvPlaylist.Rows)
            {
                var image = row.Cells["colImage"].Value as Image;
                image?.Dispose();
                row.Cells["colImage"].Value = null;
            }
        }

        private void DisposeImages(System.Collections.Generic.IEnumerable<Image> images)
        {
            foreach (var image in images) image?.Dispose();
        }

        private void Shuffle_CheckedChanged(object sender, EventArgs e)
        {
            if (!Shuffle.Checked) { lblStatus.Text = AppText.ShuffleOff; return; }
            if (_playlist == null || _playlist.Count <= 1) { lblStatus.Text = AppText.ShuffleInsufficient; return; }
            _playlist.Shuffle();
            RefreshPlaylistUI();
            lblStatus.Text = AppText.ShuffleDone;
        }

        private void btnSort_Click(object sender, EventArgs e)
        {
            _playlist.Sort();
            RefreshPlaylistUI();
            lblStatus.Text = AppText.SortDone;
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            string kw = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(kw)) { RefreshPlaylistUI(); return; }
            var kq = _playlist.Find(kw);
            RenderPlaylist(kq, $"{kq.Count}/{_playlist.Count} bài hát");
        }

        private void chkRepeatAll_CheckedChanged(object sender, EventArgs e)
        {
            _playlist.IsRepeatAll = chkRepeatAll.Checked;
        }
    }
}
