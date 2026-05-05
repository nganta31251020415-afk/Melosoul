using System;
using System.Collections.Generic;
using System.Linq;

namespace Melosoul.Services
{
    public sealed class PlaylistManager : IDisposable
    {
        private readonly PlaylistLinkedList _songs;

        public PlaylistManager()
        {
            _songs = new PlaylistLinkedList();
        }

        public int Count
        {
            get { return _songs.Count; }
        }

        public Song CurrentSong
        {
            get { return _songs.CurrentSong; }
        }

        public IReadOnlyList<Song> Songs
        {
            get { return _songs.ToList(); }
        }

        public bool IsRepeatAll
        {
            get { return _songs.IsRepeatAll; }
            set { _songs.IsRepeatAll = value; }
        }

        public void Add(Song song)
        {
            if (song == null)
                throw new ArgumentNullException(nameof(song));

            if (string.IsNullOrWhiteSpace(song.Id))
                song.Id = Guid.NewGuid().ToString();

            _songs.AddLast(song);
        }

        public void AddRange(IEnumerable<Song> songs)
        {
            if (songs == null)
                return;

            foreach (Song song in songs.Where(s => s != null))
                Add(song);
        }

        public bool Remove(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId))
                return false;

            return _songs.Remove(songId);
        }

        public void Reset()
        {
            _songs.Clear();
        }

        public Song Next()
        {
            return _songs.Next();
        }

        public Song Previous()
        {
            return _songs.Prev();
        }

        public bool MoveTo(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId))
                return false;

            return _songs.MoveTo(songId);
        }

        public IReadOnlyList<Song> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return Songs;

            return _songs.Find(keyword);
        }

        public void Sort()
        {
            _songs.Sort();
        }

        public void Shuffle()
        {
            _songs.Shuffle();
        }

        public void Dispose()
        {
            _songs.Dispose();
        }
    }
}

