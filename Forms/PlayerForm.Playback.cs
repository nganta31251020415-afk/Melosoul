using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Melosoul.Services;
using WMPLib;

namespace Melosoul
{
    public partial class PlayerForm
    {
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

        private void InitializeProgressBarSeeking()
        {
            progressBar.MouseMove += progressBar_MouseMove;
            progressBar.MouseUp += progressBar_MouseUp;
            progressBar.MouseLeave += progressBar_MouseLeave;
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
            _isSeekingProgressBar = true;
            SeekProgressBarTo(e.X);
        }

        private void progressBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSeekingProgressBar) return;
            if ((Control.MouseButtons & MouseButtons.Left) != MouseButtons.Left) return;
            SeekProgressBarTo(e.X);
        }

        private void progressBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _isSeekingProgressBar = false;
        }

        private void progressBar_MouseLeave(object sender, EventArgs e)
        {
            if ((Control.MouseButtons & MouseButtons.Left) != MouseButtons.Left)
                _isSeekingProgressBar = false;
        }

        private void SeekProgressBarTo(int mouseX)
        {
            if (_wmp == null || _wmp.currentMedia == null || _wmp.currentMedia.duration <= 0)
                return;

            double ratio = Math.Max(0, Math.Min(1, (double)mouseX / progressBar.Width));
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
            if (NewState == (int)WMPPlayState.wmppsMediaEnded)
            {
                QueueAutoNext(300);
                return;
            }

            if (NewState == (int)WMPPlayState.wmppsPlaying)
            {
                if (InvokeRequired) Invoke(new Action(() => SetPlayButtonState(true)));
                else SetPlayButtonState(true);
            }
            else if (NewState == (int)WMPPlayState.wmppsPaused || NewState == (int)WMPPlayState.wmppsStopped)
            {
                if (InvokeRequired) Invoke(new Action(() => SetPlayButtonState(false)));
                else SetPlayButtonState(false);
            }
        }

        private void WMP_MediaError(object pMediaObject)
        {
            if (InvokeRequired)
                Invoke(new Action(() => MessageBox.Show(AppText.MediaCannotPlay, AppText.MediaErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            else
                MessageBox.Show(AppText.MediaCannotPlay, AppText.MediaErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void trackVolume_ValueChanged(object sender, EventArgs e)
        {
            if (_wmp != null)
                _wmp.settings.volume = trackVolume.Value;
        }

        private void QueueAutoNext(int delayMs)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (System.Threading.Interlocked.Exchange(ref _autoNextQueued, 1) == 1) return;

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
                MessageBox.Show("File không tồn tại:\n" + song.FilePath, AppText.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show("Không thể phát:\n" + ex.Message, AppText.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            catch { }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            TogglePlayPause();
        }

        private void TogglePlayPause()
        {
            if (_wmp == null) return;
            if (_wmp.playState == WMPPlayState.wmppsPlaying) { _wmp.controls.pause(); SetPlayButtonState(false); return; }
            if (_wmp.playState == WMPPlayState.wmppsPaused) { _wmp.controls.play(); SetPlayButtonState(true); return; }
            Song song = _playlist.CurrentSong;
            if (song == null) MessageBox.Show(AppText.PlaylistEmptyShort, AppText.NoticeTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else PlaySong(song);
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            Song song = _playlist.Next();
            if (song == null) MessageBox.Show(AppText.EndOfPlaylist, AppText.NoticeTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else { PlaySong(song); RefreshPlaylistUI(); }
        }

        private void btnPrev_Click(object sender, EventArgs e)
        {
            Song song = _playlist.Prev();
            if (song == null) MessageBox.Show(AppText.FirstSongNotice, AppText.NoticeTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else { PlaySong(song); RefreshPlaylistUI(); }
        }
    }
}
