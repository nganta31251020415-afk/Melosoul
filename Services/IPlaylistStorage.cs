using System.Collections.Generic;

namespace Melosoul.Services
{
    public interface IPlaylistStorage
    {
        void Save(PlaylistLinkedList playlist);
        List<Song> Load();
    }
}
