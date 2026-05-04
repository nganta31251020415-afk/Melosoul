using System;
using System.IO;
using WMPLib;

namespace Melosoul.Services
{
    public sealed class AudioPlayer : IDisposable
    {
        private readonly WindowsMediaPlayer _player;

        public AudioPlayer()
        {
            _player = new WindowsMediaPlayer
            {
                uiMode = "invisible"
            };

            _player.settings.autoStart = false;
        }

        public int Volume
        {
            get { return _player.settings.volume; }
            set { _player.settings.volume = Math.Max(0, Math.Min(100, value)); }
        }

        public bool IsPlaying
        {
            get { return _player.playState == WMPPlayState.wmppsPlaying; }
        }

        public void LoadFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Audio file was not found.", filePath);

            _player.URL = filePath;
        }

        public void Play(Song song)
        {
            if (song == null)
                throw new ArgumentNullException(nameof(song));

            LoadFile(song.FilePath);
            Play();
        }

        public void Play()
        {
            _player.controls.play();
        }

        public void Pause()
        {
            _player.controls.pause();
        }

        public void Stop()
        {
            _player.controls.stop();
        }

        public void Dispose()
        {
            try
            {
                _player.controls.stop();
                System.Runtime.InteropServices.Marshal.ReleaseComObject(_player);
            }
            catch
            {
            }
        }
    }
}
