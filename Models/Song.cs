using System;
using System.Runtime.Serialization;

[DataContract]
public class Song
{
    public Song()
    {
        Id = Guid.NewGuid().ToString();
        Title = string.Empty;
        Artist = string.Empty;
        Duration = string.Empty;
        FilePath = string.Empty;
    }

    public Song(string id, string title, string artist, string filePath)
        : this(id, title, artist, string.Empty, filePath)
    {
    }

    public Song(string id, string title, string artist, string duration, string filePath)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id;
        Title = title ?? string.Empty;
        Artist = artist ?? string.Empty;
        Duration = duration ?? string.Empty;
        FilePath = filePath ?? string.Empty;
    }

    [DataMember(Name = "id")]
    public string Id { get; set; }

    public string ID
    {
        get { return Id; }
        set { Id = value; }
    }

    [DataMember(Name = "title")]
    public string Title { get; set; }

    [DataMember(Name = "artist")]
    public string Artist { get; set; }

    [DataMember(Name = "duration")]
    public string Duration { get; set; }

    [DataMember(Name = "filePath")]
    public string FilePath { get; set; }
}
