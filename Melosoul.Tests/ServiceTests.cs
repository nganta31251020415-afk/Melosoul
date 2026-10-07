using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Melosoul.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Melosoul.Tests
{
    [TestClass]
    public class ServiceTests
    {
        [TestMethod]
        public void AutoSave_ParseV2Line_ReturnsSong()
        {
            var service = new AutoSaveService();
            var method = typeof(AutoSaveService).GetMethod(
                "ParseSaveLine",
                BindingFlags.NonPublic | BindingFlags.Instance);

            string title = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Song A"));
            string artist = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Artist B"));
            string path = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(@"C:\tmp\a.mp3"));
            string line = $"v2|{title}|{artist}|{path}";

            var song = method.Invoke(service, new object[] { line }) as Song;

            Assert.IsNotNull(song);
            Assert.AreEqual("Song A", song.Title);
            Assert.AreEqual("Artist B", song.Artist);
            Assert.AreEqual(@"C:\tmp\a.mp3", song.FilePath);
        }

        [TestMethod]
        public async Task FileImport_Deduplicate_Works()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "melosoul-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string f1 = Path.Combine(tempDir, "a.mp3");
                string f2 = Path.Combine(tempDir, "b.mp3");
                File.WriteAllText(f1, "x");
                File.WriteAllText(f2, "y");

                var metadata = new MediaMetadataService();
                var loader = new PlaylistLoaderService(metadata);
                var import = new FileImportService(loader);

                var result = await import.ImportAsync(
                    new[] { f1, f2, f1, Path.Combine(tempDir, "img.png") },
                    new[] { f2 });

                Assert.AreEqual(1, result.Songs.Count);
                Assert.AreEqual(2, result.DuplicateCount);
                Assert.AreEqual(f1, result.Songs[0].FilePath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void DurationService_Fallback_OnMissingFile()
        {
            var service = new DurationService();
            string duration = service.Resolve(@"Z:\this\file\does-not-exist.mp3");
            Assert.AreEqual("--:--", duration);
        }
    }
}
