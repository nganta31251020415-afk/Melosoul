using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Melosoul.Services
{
    public class AlbumArtService
    {
        private static readonly string[] ImageExtensions =
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        public Bitmap CreateAlbumCoverImage(Song song, int maxSize)
        {
            Bitmap embeddedImage = CreateEmbeddedMp3Image(song?.FilePath, maxSize);
            if (embeddedImage != null)
                return embeddedImage;

            string path = FindSongSpecificCoverImagePath(song);
            if (path == null)
                return null;

            try
            {
                using (var fs = File.OpenRead(path))
                using (var img = Image.FromStream(fs))
                {
                    return ResizeImageToFit(img, maxSize);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        public Image CreatePlaylistThumbnail(Song song)
        {
            var embeddedImage = CreateEmbeddedMp3Thumbnail(song?.FilePath);
            if (embeddedImage != null)
                return embeddedImage;

            string path = FindSongSpecificCoverImagePath(song);
            if (path == null)
                return null;

            try
            {
                using (var fs = File.OpenRead(path))
                using (var img = Image.FromStream(fs))
                {
                    return new Bitmap(img, 36, 36);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        public Bitmap CreateDefaultMusicNoteImage(int size)
        {
            int canvasSize = Math.Max(64, size);
            var bmp = new Bitmap(canvasSize, canvasSize);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(42, 42, 42));
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (var staffPen = new Pen(Color.FromArgb(70, 70, 70), 2f))
                {
                    int left = canvasSize / 6;
                    int right = canvasSize - canvasSize / 6;
                    int mid = canvasSize / 2;
                    g.DrawLine(staffPen, left, mid - 24, right, mid - 24);
                    g.DrawLine(staffPen, left, mid - 10, right, mid - 10);
                    g.DrawLine(staffPen, left, mid + 4, right, mid + 4);
                }

                using (var noteBrush = new SolidBrush(Color.FromArgb(214, 125, 162)))
                using (var notePen = new Pen(Color.FromArgb(255, 175, 200), 3f))
                {
                    int stemX = (int)(canvasSize * 0.58);
                    int stemTop = (int)(canvasSize * 0.22);
                    int stemBottom = (int)(canvasSize * 0.62);
                    g.DrawLine(notePen, stemX, stemTop, stemX, stemBottom);

                    int headW = (int)(canvasSize * 0.22);
                    int headH = (int)(canvasSize * 0.16);
                    int headX = stemX - headW;
                    int headY = stemBottom - headH / 2;
                    g.FillEllipse(noteBrush, headX, headY, headW, headH);
                    g.DrawEllipse(notePen, headX, headY, headW, headH);

                    Point[] flag =
                    {
                        new Point(stemX, stemTop),
                        new Point((int)(stemX + canvasSize * 0.18), (int)(stemTop + canvasSize * 0.04)),
                        new Point(stemX, (int)(stemTop + canvasSize * 0.12))
                    };
                    g.FillPolygon(noteBrush, flag);
                }
            }

            return bmp;
        }

        private Bitmap CreateEmbeddedMp3Image(string filePath, int maxSize)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            if (!string.Equals(Path.GetExtension(filePath), ".mp3", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                byte[] imageBytes = ReadEmbeddedMp3ImageBytes(filePath);
                if (imageBytes == null || imageBytes.Length == 0)
                    return null;

                using (var ms = new MemoryStream(imageBytes))
                using (var img = Image.FromStream(ms))
                {
                    return ResizeImageToFit(img, maxSize);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private Image CreateEmbeddedMp3Thumbnail(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            if (!string.Equals(Path.GetExtension(filePath), ".mp3", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                byte[] imageBytes = ReadEmbeddedMp3ImageBytes(filePath);
                if (imageBytes == null || imageBytes.Length == 0)
                    return null;

                using (var ms = new MemoryStream(imageBytes))
                using (var img = Image.FromStream(ms))
                {
                    return new Bitmap(img, 36, 36);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Melosoul] {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private Bitmap ResizeImageToFit(Image image, int maxSize)
        {
            if (image.Width > maxSize || image.Height > maxSize)
            {
                float ratio = Math.Min((float)maxSize / image.Width, (float)maxSize / image.Height);
                int newWidth = Math.Max(1, (int)(image.Width * ratio));
                int newHeight = Math.Max(1, (int)(image.Height * ratio));
                return new Bitmap(image, newWidth, newHeight);
            }

            return new Bitmap(image);
        }

        private string FindSongSpecificCoverImagePath(Song song)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.FilePath))
                return null;

            string dir = Path.GetDirectoryName(song.FilePath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return null;

            string fileNameNoExt = Path.GetFileNameWithoutExtension(song.FilePath);
            foreach (var ext in ImageExtensions)
            {
                string path = Path.Combine(dir, fileNameNoExt + ext);
                if (File.Exists(path))
                    return path;
            }

            string normalizedTarget = NormalizeName(fileNameNoExt);
            if (string.IsNullOrWhiteSpace(normalizedTarget))
                return null;

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (!ImageExtensions.Contains(ext))
                    continue;

                string name = Path.GetFileNameWithoutExtension(file);
                string normalizedName = NormalizeName(name);
                if (normalizedName.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedName))
                    return file;
            }

            return null;
        }

        private string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var cleaned = new StringBuilder();
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    cleaned.Append(c);
            }

            return cleaned.ToString();
        }

        private byte[] ReadEmbeddedMp3ImageBytes(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            {
                if (fs.Length < 10)
                    return null;

                byte[] header = new byte[10];
                if (fs.Read(header, 0, header.Length) != header.Length)
                    return null;

                if (header[0] != 'I' || header[1] != 'D' || header[2] != '3')
                    return null;

                int majorVersion = header[3];
                int flags = header[5];
                int tagSize = ReadSynchsafeInt(header, 6);
                if (tagSize <= 0 || tagSize > fs.Length - 10)
                    return null;

                byte[] tagData = new byte[tagSize];
                if (fs.Read(tagData, 0, tagData.Length) != tagData.Length)
                    return null;

                if ((flags & 0x80) != 0)
                    tagData = RemoveUnsynchronisation(tagData);

                if (majorVersion == 2)
                    return ReadId3v22ImageBytes(tagData);

                if (majorVersion == 3 || majorVersion == 4)
                    return ReadId3v23Or24ImageBytes(tagData, majorVersion, (flags & 0x40) != 0);
            }

            return null;
        }

        private byte[] ReadId3v23Or24ImageBytes(byte[] tagData, int majorVersion, bool hasExtendedHeader)
        {
            int index = 0;

            if (hasExtendedHeader && tagData.Length >= 4)
            {
                int extendedSize = majorVersion == 4
                    ? ReadSynchsafeInt(tagData, 0)
                    : ReadBigEndianInt(tagData, 0);
                index = Math.Max(0, Math.Min(tagData.Length, extendedSize + (majorVersion == 3 ? 4 : 0)));
            }

            while (index + 10 <= tagData.Length)
            {
                string frameId = Encoding.ASCII.GetString(tagData, index, 4);
                if (string.IsNullOrWhiteSpace(frameId) || frameId.Trim('\0').Length == 0)
                    break;

                int frameSize = majorVersion == 4
                    ? ReadSynchsafeInt(tagData, index + 4)
                    : ReadBigEndianInt(tagData, index + 4);
                if (frameSize <= 0 || index + 10 + frameSize > tagData.Length)
                    break;

                if (frameId == "APIC")
                    return ExtractApicImageBytes(tagData, index + 10, frameSize);

                index += 10 + frameSize;
            }

            return null;
        }

        private byte[] ReadId3v22ImageBytes(byte[] tagData)
        {
            int index = 0;
            while (index + 6 <= tagData.Length)
            {
                string frameId = Encoding.ASCII.GetString(tagData, index, 3);
                if (string.IsNullOrWhiteSpace(frameId) || frameId.Trim('\0').Length == 0)
                    break;

                int frameSize = (tagData[index + 3] << 16) | (tagData[index + 4] << 8) | tagData[index + 5];
                if (frameSize <= 0 || index + 6 + frameSize > tagData.Length)
                    break;

                if (frameId == "PIC")
                    return ExtractPicImageBytes(tagData, index + 6, frameSize);

                index += 6 + frameSize;
            }

            return null;
        }

        private byte[] ExtractApicImageBytes(byte[] data, int start, int size)
        {
            int end = start + size;
            int index = start;
            if (index >= end)
                return null;

            byte encoding = data[index++];

            while (index < end && data[index] != 0)
                index++;
            index++;

            if (index >= end)
                return null;

            index++;
            index = SkipEncodedTerminatedString(data, index, end, encoding);
            if (index >= end)
                return null;

            return CopyRange(data, index, end - index);
        }

        private byte[] ExtractPicImageBytes(byte[] data, int start, int size)
        {
            int end = start + size;
            int index = start;
            if (index + 5 >= end)
                return null;

            byte encoding = data[index++];
            index += 3;
            index++;
            index = SkipEncodedTerminatedString(data, index, end, encoding);
            if (index >= end)
                return null;

            return CopyRange(data, index, end - index);
        }

        private int SkipEncodedTerminatedString(byte[] data, int index, int end, byte encoding)
        {
            if (encoding == 1 || encoding == 2)
            {
                while (index + 1 < end)
                {
                    if (data[index] == 0 && data[index + 1] == 0)
                        return index + 2;
                    index += 2;
                }
                return end;
            }

            while (index < end && data[index] != 0)
                index++;
            return Math.Min(end, index + 1);
        }

        private int ReadSynchsafeInt(byte[] data, int index)
        {
            return ((data[index] & 0x7F) << 21) |
                   ((data[index + 1] & 0x7F) << 14) |
                   ((data[index + 2] & 0x7F) << 7) |
                   (data[index + 3] & 0x7F);
        }

        private int ReadBigEndianInt(byte[] data, int index)
        {
            return (data[index] << 24) |
                   (data[index + 1] << 16) |
                   (data[index + 2] << 8) |
                   data[index + 3];
        }

        private byte[] RemoveUnsynchronisation(byte[] data)
        {
            var cleaned = new System.Collections.Generic.List<byte>(data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                if (i + 1 < data.Length && data[i] == 0xFF && data[i + 1] == 0x00)
                {
                    cleaned.Add(0xFF);
                    i++;
                }
                else
                {
                    cleaned.Add(data[i]);
                }
            }

            return cleaned.ToArray();
        }

        private byte[] CopyRange(byte[] data, int start, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(data, start, result, 0, length);
            return result;
        }
    }
}
