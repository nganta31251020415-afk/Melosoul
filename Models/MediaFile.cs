using System;
using System.Runtime.Serialization;

[DataContract]
public abstract class MediaFile
{
    protected MediaFile()
    {
        Id = Guid.NewGuid().ToString();
        Title = string.Empty;
        FilePath = string.Empty;
    }

    protected MediaFile(string id, string title, string filePath)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id;
        Title = title ?? string.Empty;
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

    [DataMember(Name = "filePath")]
    public string FilePath { get; set; }

    public abstract string GetDisplayText();
}
