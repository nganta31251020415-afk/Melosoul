using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Melosoul.Services
{
    public class PlaylistLoaderService
    {
        private static readonly string[] SupportedExtensions =
            { ".mp3", ".mp4", ".wav", ".wma", ".aac", ".flac", ".m4a" };

        private readonly MediaMetadataService _metadataService;

        public PlaylistLoaderService(MediaMetadataService metadataService)
        {
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }

        public Task<(List<Song> Songs, int DuplicateCount)> CreateSongsAsync(
            IEnumerable<string> files,
            ISet<string> existingPaths)
        {
            return Task.Run(() =>
            {
                var list = new List<Song>();
                int duplicateCount = 0;
                var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in files ?? Enumerable.Empty<string>())
                {
                    if (!IsValidAudioFile(file))
                        continue;

                    if (existingPaths.Contains(file) || seenFiles.Contains(file))
                    {
                        duplicateCount++;
                        continue;
                    }

                    seenFiles.Add(file);
                    var song = _metadataService.CreateSongFromFile(file);
                    if (song != null)
                        list.Add(song);
                }

                return (list, duplicateCount);
            });
        }

        private static bool IsValidAudioFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            if (!File.Exists(filePath))
                return false;

            string ext = Path.GetExtension(filePath);
            return SupportedExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
