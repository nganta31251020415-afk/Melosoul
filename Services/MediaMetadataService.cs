using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using WMPLib;

namespace Melosoul.Services
{
    public class MediaMetadataService
    {
        private readonly ConcurrentDictionary<string, string> _durationCache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string GetDuration(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return "--:--";

            if (_durationCache.TryGetValue(filePath, out string cachedDuration))
                return cachedDuration;

            if (!File.Exists(filePath))
                return "--:--";

            bool hasTagDuration = TryGetTagLibDuration(filePath, out double tagDuration);
            bool hasWmpDuration = TryGetWmpDuration(filePath, out double wmpDuration);

            double finalDuration = 0;
            if (hasTagDuration && hasWmpDuration)
            {
                double diff = Math.Abs(tagDuration - wmpDuration);
                double minDuration = Math.Min(tagDuration, wmpDuration);
                if (minDuration > 0 && diff / minDuration > 0.15)
                {
                    if (TryProbeDurationByPlayback(filePath, out double probeDuration) && probeDuration > 0)
                    {
                        finalDuration = Math.Abs(probeDuration - tagDuration) <= Math.Abs(probeDuration - wmpDuration)
                            ? tagDuration
                            : wmpDuration;
                    }
                    else if (string.Equals(Path.GetExtension(filePath), ".mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        finalDuration = tagDuration;
                    }
                    else
                    {
                        finalDuration = wmpDuration;
                    }
                }
                else
                {
                    finalDuration = wmpDuration > 0 ? wmpDuration : tagDuration;
                }
            }
            else if (hasWmpDuration)
            {
                finalDuration = wmpDuration;
            }
            else if (hasTagDuration)
            {
                finalDuration = tagDuration;
            }

            if (finalDuration > 0)
            {
                string durationText = FormatTime(finalDuration);
                _durationCache[filePath] = durationText;
                return durationText;
            }

            return "--:--";
        }

        public Song CreateSongFromFile(string filePath)
        {
            string title = Path.GetFileNameWithoutExtension(filePath);
            string artist = string.Empty;

            try
            {
                using (var tagFile = TagLib.File.Create(filePath))
                {
                    if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title))
                        title = tagFile.Tag.Title.Trim();

                    if (tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0)
                        artist = tagFile.Tag.Performers[0].Trim();

                    if (string.IsNullOrWhiteSpace(artist) && tagFile.Tag.AlbumArtists != null && tagFile.Tag.AlbumArtists.Length > 0)
                        artist = tagFile.Tag.AlbumArtists[0].Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }

            title = title?.Trim();
            artist = artist?.Trim();

            return new Song(
                Guid.NewGuid().ToString(),
                string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(filePath) : title,
                artist ?? string.Empty,
                string.Empty,
                filePath);
        }

        private bool TryGetTagLibDuration(string filePath, out double durationSeconds)
        {
            durationSeconds = 0;
            try
            {
                using (var tagFile = TagLib.File.Create(filePath))
                {
                    durationSeconds = tagFile.Properties.Duration.TotalSeconds;
                    return durationSeconds > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private bool TryGetWmpDuration(string filePath, out double durationSeconds)
        {
            durationSeconds = 0;
            WindowsMediaPlayer player = null;
            IWMPMedia media = null;
            try
            {
                player = new WindowsMediaPlayer();
                media = player.newMedia(filePath);
                if (media == null)
                    return false;

                durationSeconds = media.duration;
                return durationSeconds > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            finally
            {
                if (media != null)
                    Marshal.ReleaseComObject(media);
                if (player != null)
                    Marshal.ReleaseComObject(player);
            }
        }

        private bool TryProbeDurationByPlayback(string filePath, out double durationSeconds)
        {
            durationSeconds = 0;
            string durationText = ProbeDurationByPlayback(filePath);
            if (string.IsNullOrWhiteSpace(durationText) || durationText == "--:--")
                return false;

            var parts = durationText.Split(':');
            if (parts.Length != 2)
                return false;

            if (int.TryParse(parts[0], out int min) && int.TryParse(parts[1], out int sec))
            {
                durationSeconds = min * 60 + sec;
                return durationSeconds > 0;
            }

            return false;
        }

        private string ProbeDurationByPlayback(string filePath)
        {
            WindowsMediaPlayer probePlayer = null;
            try
            {
                if (!File.Exists(filePath))
                    return "--:--";

                probePlayer = new WindowsMediaPlayer();
                probePlayer.settings.volume = 0;
                probePlayer.settings.autoStart = false;
                probePlayer.URL = filePath;
                probePlayer.controls.play();

                for (int i = 0; i < 40; i++)
                {
                    double dur = 0;
                    try
                    {
                        dur = probePlayer.currentMedia?.duration ?? 0;
                    }
                    catch
                    {
                        dur = 0;
                    }

                    if (dur > 0)
                    {
                        probePlayer.controls.stop();
                        return FormatTime(dur);
                    }

                    System.Threading.Thread.Sleep(50);
                }

                probePlayer.controls.stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (probePlayer != null)
                    Marshal.ReleaseComObject(probePlayer);
            }

            return "--:--";
        }

        private string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                return "0:00";

            int totalSeconds = (int)Math.Floor(seconds);
            if (totalSeconds < 0)
                totalSeconds = 0;

            int min = totalSeconds / 60;
            int sec = totalSeconds % 60;
            return $"{min}:{sec:D2}";
        }

        public void CacheDuration(string filePath, string durationText)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(durationText))
                return;

            _durationCache[filePath] = durationText;
        }

        public void ClearDurationCache()
        {
            _durationCache.Clear();
        }
    }
}
