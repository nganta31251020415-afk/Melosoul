using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Melosoul.Tests
{
    [TestClass]
    public class PlaylistLinkedListTests
    {
        private static Song S(string id, string title, string artist = "A")
            => new Song(id, title, artist, @"C:\tmp\" + id + ".mp3");

        [TestMethod]
        public void Remove_CurrentHeadTail_UpdatesPointersAndCount()
        {
            var p = new PlaylistLinkedList();
            p.AddLast(S("1", "B"));
            p.AddLast(S("2", "C"));
            p.AddLast(S("3", "D"));

            Assert.IsTrue(p.MoveTo("1"));
            Assert.IsTrue(p.Remove("1"));
            Assert.AreEqual("2", p.CurrentSong.ID);
            Assert.AreEqual(2, p.Count);

            Assert.IsTrue(p.Remove("3"));
            Assert.AreEqual(1, p.Count);
            var list = p.ToList();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("2", list[0].ID);
        }

        [TestMethod]
        public void NextPrev_RespectRepeatFlag()
        {
            var p = new PlaylistLinkedList();
            p.AddLast(S("1", "A"));
            p.AddLast(S("2", "B"));

            p.IsRepeatAll = false;
            Assert.AreEqual("2", p.Next().ID);
            Assert.IsNull(p.Next());
            Assert.AreEqual("1", p.Prev().ID);
            Assert.IsNull(p.Prev());

            p.IsRepeatAll = true;
            Assert.AreEqual("2", p.Next().ID);
            Assert.AreEqual("1", p.Next().ID);
            Assert.AreEqual("2", p.Prev().ID);
        }

        [TestMethod]
        public void Sort_ByTitleThenArtist_StableForEqualKeys()
        {
            var p = new PlaylistLinkedList();
            p.AddLast(S("1", "Same", "B"));
            p.AddLast(S("2", "Same", "A"));
            p.AddLast(S("3", "Alpha", "Z"));
            p.AddLast(S("4", "Same", "A"));

            p.Sort();
            List<Song> list = p.ToList();

            Assert.AreEqual("3", list[0].ID);
            Assert.AreEqual("2", list[1].ID);
            Assert.AreEqual("4", list[2].ID);
            Assert.AreEqual("1", list[3].ID);
        }

        [TestMethod]
        public void Shuffle_PreservesAllNodes_NoDuplicates()
        {
            var p = new PlaylistLinkedList();
            for (int i = 0; i < 30; i++)
                p.AddLast(S(i.ToString(), "T" + i));

            var before = new HashSet<string>();
            foreach (var s in p.ToList()) before.Add(s.ID);

            p.Shuffle();
            var after = p.ToList();

            Assert.AreEqual(30, after.Count);
            var seen = new HashSet<string>();
            foreach (var s in after) seen.Add(s.ID);
            Assert.AreEqual(before.Count, seen.Count);
            foreach (var id in before)
                Assert.IsTrue(seen.Contains(id));
        }

        [TestMethod]
        public void Stress_BasicOperations_5000Songs()
        {
            var p = new PlaylistLinkedList();
            const int n = 5000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++)
                p.AddLast(S(i.ToString(), "Title " + (i % 200), "Artist " + (i % 50)));
            sw.Stop();

            Assert.AreEqual(n, p.Count);

            sw.Restart();
            p.Sort();
            sw.Stop();
            Assert.IsTrue(sw.ElapsedMilliseconds < 3000, "Sort too slow for 5000 nodes.");

            sw.Restart();
            var found = p.Find("Title 1");
            sw.Stop();
            Assert.IsTrue(found.Count > 0);
            Assert.IsTrue(sw.ElapsedMilliseconds < 1000, "Find too slow.");

            sw.Restart();
            p.Shuffle();
            sw.Stop();
            Assert.AreEqual(n, p.Count);
        }
    }
}
