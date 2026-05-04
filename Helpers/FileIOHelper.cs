using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace Melosoul.Helpers
{
    public static class FileIOHelper
    {
        public static Task<List<Song>> LoadSongsAsync(string filePath)
        {
            return Task.Run(() =>
            {
                ValidateFilePath(filePath);

                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension == ".json")
                    return ReadJson(filePath);

                if (extension == ".txt")
                    return ReadText(filePath);

                throw new NotSupportedException("Only .json and .txt playlists are supported.");
            });
        }

        public static Task SaveSongsAsync(string filePath, IEnumerable<Song> songs)
        {
            return Task.Run(() =>
            {
                ValidateFilePath(filePath);
                EnsureDirectoryExists(filePath);

                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension == ".json")
                {
                    WriteJson(filePath, songs);
                    return;
                }

                if (extension == ".txt")
                {
                    WriteText(filePath, songs);
                    return;
                }

                throw new NotSupportedException("Only .json and .txt playlists are supported.");
            });
        }

        private static List<Song> ReadJson(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<Song>));
                return serializer.ReadObject(stream) as List<Song> ?? new List<Song>();
            }
        }

        private static void WriteJson(string filePath, IEnumerable<Song> songs)
        {
            using (var stream = File.Create(filePath))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<Song>));
                serializer.WriteObject(stream, new List<Song>(songs ?? new List<Song>()));
            }
        }

        private static List<Song> ReadText(string filePath)
        {
            var songs = new List<Song>();
            foreach (string line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split('|');
                if (parts.Length < 3)
                    continue;

                Song song = MapTextLine(parts);
                songs.Add(song);
            }

            return songs;
        }

        private static Song MapTextLine(string[] parts)
        {
            if (parts.Length >= 5)
            {
                return new Song(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    parts[2].Trim(),
                    parts[3].Trim(),
                    parts[4].Trim());
            }

            if (parts.Length == 4)
            {
                return new Song(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    parts[2].Trim(),
                    parts[3].Trim());
            }

            return new Song(
                Guid.NewGuid().ToString(),
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim());
        }

        private static void WriteText(string filePath, IEnumerable<Song> songs)
        {
            var lines = new List<string>();
            foreach (Song song in songs ?? new List<Song>())
            {
                lines.Add(string.Join("|", new[]
                {
                    Clean(song.Id),
                    Clean(song.Title),
                    Clean(song.Artist),
                    Clean(song.Duration),
                    Clean(song.FilePath)
                }));
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace("|", " ").Trim();
        }

        private static void ValidateFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
