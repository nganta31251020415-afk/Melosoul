using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using WMPLib;

namespace Melosoul.Services
{
    public class DurationService
    {
        private readonly ConcurrentDictionary<string, string> _cache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Resolve(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return "--:--";

            if (_cache.TryGetValue(filePath, out string cached))
                return cached;

            string resolved = ResolveInternal(filePath);
            if (resolved != "--:--")
                _cache[filePath] = resolved;
            return resolved;
        }

        public void Remove(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
                _cache.TryRemove(filePath, out _);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private string ResolveInternal(string filePath)
        {
            if (!File.Exists(filePath))
                return "--:--";

            string wmp = TryGetDurationFromWmp(filePath);
            if (wmp != "--:--")
                return wmp;

            try
            {
                using (var f = TagLib.File.Create(filePath))
                {
                    return FormatDurationSeconds(f.Properties.Duration.TotalSeconds);
                }
            }
            catch
            {
                return "--:--";
            }
        }

        private string TryGetDurationFromWmp(string filePath)
        {
            WindowsMediaPlayer player = null;
            IWMPMedia media = null;
            try
            {
                player = new WindowsMediaPlayer();
                media = player.newMedia(filePath);
                if (media == null)
                    return "--:--";

                string direct = FormatDurationSeconds(media.duration);
                if (direct != "--:--")
                    return direct;

                string rawDuration = media.getItemInfo("Duration");
                if (TimeSpan.TryParse(rawDuration, out TimeSpan parsed))
                    return FormatTime(parsed.TotalSeconds);

                return "--:--";
            }
            catch
            {
                return "--:--";
            }
            finally
            {
                if (media != null)
                    Marshal.ReleaseComObject(media);
                if (player != null)
                    Marshal.ReleaseComObject(player);
            }
        }

        private static string FormatDurationSeconds(double duration)
        {
            if (duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration))
                return "--:--";

            return FormatTime(duration);
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0)
                seconds = 0;
            int min = (int)seconds / 60;
            int sec = (int)seconds % 60;
            return $"{min}:{sec:D2}";
        }
    }
}
