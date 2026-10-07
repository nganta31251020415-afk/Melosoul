using System;
using System.Runtime.Serialization;

[DataContract]
public class Song : MediaFile
{
    public Song() : base()
    {
        Artist = string.Empty;
        Duration = string.Empty;
    }

    public Song(string id, string title, string artist, string filePath)
        : this(id, title, artist, string.Empty, filePath)
    {
    }

    public Song(string id, string title, string artist, string duration, string filePath)
        : base(id, title, filePath)
    {
        Artist = artist ?? string.Empty;
        Duration = duration ?? string.Empty;
    }

    [DataMember(Name = "artist")]
    public string Artist { get; set; }

    [DataMember(Name = "duration")]
    public string Duration { get; set; }

    public override string GetDisplayText()
    {
        return $"{Title} - {Artist}";
    }
}
