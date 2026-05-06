using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Melosoul.Services
{
    public class AutoSaveService
    {
        private readonly string _autoSavePath;

        public AutoSaveService()
        {
            _autoSavePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Melosoul",
                "autosave.txt");
        }

        public void Save(PlaylistLinkedList playlist)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_autoSavePath));

                var lines = new List<string>();
                if (playlist != null)
                {
                    foreach (var song in playlist.ToList())
                        lines.Add(CreateSaveLine(song));
                }

                File.WriteAllLines(_autoSavePath, lines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }
        }

        public List<Song> Load()
        {
            var songs = new List<Song>();

            try
            {
                if (!File.Exists(_autoSavePath))
                    return songs;

                foreach (var line in File.ReadAllLines(_autoSavePath))
                {
                    Song song = ParseSaveLine(line);
                    if (song != null)
                        songs.Add(song);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
            }

            return songs;
        }

        private string CreateSaveLine(Song song)
        {
            return "v2|" +
                   Encode(song?.Title) + "|" +
                   Encode(song?.Artist) + "|" +
                   Encode(song?.FilePath);
        }

        private Song ParseSaveLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var parts = line.Split('|');
            if (parts.Length < 3)
                return null;

            if (parts[0] == "v2" && parts.Length >= 4)
            {
                return new Song(
                    Guid.NewGuid().ToString(),
                    Decode(parts[1]).Trim(),
                    Decode(parts[2]).Trim(),
                    Decode(parts[3]).Trim());
            }

            return new Song(
                Guid.NewGuid().ToString(),
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim());
        }

        private string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private string Decode(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
