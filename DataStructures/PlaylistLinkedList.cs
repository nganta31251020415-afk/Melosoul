using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Melosoul
{

// Cần implement IDisposable vì chúng ta sử dụng ReaderWriterLockSlim
public class PlaylistLinkedList : IDisposable
{
    #region Node & Trie
    private class Node
    {
        public Song Data { get; set; }
        public Node Next { get; set; }
        public Node Prev { get; set; }
        public Node(Song data) => Data = data;
    }


    private class TrieNode
    {
        public readonly Dictionary<char, TrieNode> Children = new Dictionary<char, TrieNode>();
        public readonly HashSet<Node> Nodes = new HashSet<Node>();
    }


    private class Trie
    {
        private TrieNode _root = new TrieNode();


        public void Clear() => _root = new TrieNode();


        public void Insert(string word, Node node)
        {
            TrieNode cur = _root;
            foreach (char c in word)
            {
                if (!cur.Children.TryGetValue(c, out TrieNode next))
                    cur.Children[c] = next = new TrieNode();
                next.Nodes.Add(node);
                cur = next;
            }
        }


        public void Remove(string word, Node node)
        {
            TrieNode cur = _root;
            var path = new Stack<(TrieNode parent, char ch)>();


            foreach (char c in word)
            {
                if (!cur.Children.TryGetValue(c, out TrieNode next)) return;
                path.Push((cur, c));
                next.Nodes.Remove(node);
                cur = next;
            }


            while (path.Count > 0)
            {
                var (parent, ch) = path.Pop();
                TrieNode child = parent.Children[ch];


                if (child.Nodes.Count == 0 && child.Children.Count == 0)
                    parent.Children.Remove(ch);
                else
                    break;
            }
        }

        public HashSet<Node> SearchPrefix(string prefix)
        {
            TrieNode cur = _root;
            foreach (char c in prefix)
            {
                if (!cur.Children.TryGetValue(c, out TrieNode next)) return null;
                cur = next;
            }
            return cur.Nodes;
        }
    }
    #endregion

    private static readonly Random _rng = new Random();


    // Cho phép nhiều luồng cùng ĐỌC (Find, CurrentSong) mà không chờ nhau.
    // Chỉ BLOCK chặn khi có luồng GHI (Thêm, Xóa, Sort, Shuffle).
    private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);


    private Node _head;
    private Node _tail;
    private Node _current;


    private readonly Dictionary<string, Node> _idIndex;
    private readonly Trie _trie;


    public int Count { get; private set; }
    public bool IsRepeatAll { get; set; } = false;


    // Đọc trạng thái (Read Lock) để UI gọi liên tục không bị đơ app
    public Song CurrentSong
    {
        get
        {
            _rwLock.EnterReadLock();
            try { return _current?.Data; }
            finally { _rwLock.ExitReadLock(); }
        }
    }


    public PlaylistLinkedList()
    {
        _idIndex = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        _trie = new Trie();
    }


    #region Internal Helpers (Không lock vì được gọi bởi hàm đã lock)
    private static string[] Tokenize(Song song) =>
        $"{song.Title} {song.Artist}"
            .ToLowerInvariant()
            .Split(new[] { ' ', '-', '_', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);


    private void IndexSong(Node node)
    {
        _idIndex[node.Data.ID] = node;
        foreach (var token in Tokenize(node.Data))
        {
            _trie.Insert(token, node);
        }
    }

    private void RemoveFromIndex(Node node)
    {
        _idIndex.Remove(node.Data.ID);
        foreach (var token in Tokenize(node.Data))
        {
            _trie.Remove(token, node);
        }
    }
    #endregion


    #region Core Operations (Write Lock)
    public void Clear()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _head = _tail = _current = null;
            Count = 0;
            _idIndex.Clear();
            _trie.Clear();
        }
        finally { _rwLock.ExitWriteLock(); }
    }


    public void AddFirst(Song song)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var node = new Node(song);
            if (_head == null) _head = _tail = _current = node;
            else
            {
                node.Next = _head;
                _head.Prev = node;
                _head = node;
            }
            Count++;
            IndexSong(node);
        }
        finally { _rwLock.ExitWriteLock(); }
    }


    public void AddLast(Song song)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var node = new Node(song);
            if (_tail == null) _head = _tail = _current = node;
            else
            {
                _tail.Next = node;
                node.Prev = _tail;
                _tail = node;
            }
            Count++;
            IndexSong(node);
        }
        finally { _rwLock.ExitWriteLock(); }
    }


    public bool Remove(string id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (!_idIndex.TryGetValue(id, out Node node)) return false;


            if (node == _current)
                _current = node.Next ?? node.Prev;


            if (node.Prev != null) node.Prev.Next = node.Next; else _head = node.Next;
            if (node.Next != null) node.Next.Prev = node.Prev; else _tail = node.Prev;


            Count--;
            RemoveFromIndex(node);
            return true;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public bool MoveTo(string id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (!_idIndex.TryGetValue(id, out Node node)) return false;
            _current = node;
            return true;
        }
        finally { _rwLock.ExitWriteLock(); }
    }


    // Next/Prev thay đổi trạng thái _current nên dùng Write Lock
    public Song Next()
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (_current == null) return null;
            if (_current.Next != null) _current = _current.Next;
            else if (IsRepeatAll && _head != null) _current = _head;
            else return null;
            return _current.Data;
        }
        finally { _rwLock.ExitWriteLock(); }
    }


    public Song Prev()
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (_current == null) return null;
            if (_current.Prev != null) _current = _current.Prev;
            else if (IsRepeatAll && _tail != null) _current = _tail;
            else return null;
            return _current.Data;
        }
        finally { _rwLock.ExitWriteLock(); }
    }
    #endregion


    #region Search Operations (Read Lock)
    public List<Song> ToList()
    {
        _rwLock.EnterReadLock();
        try
        {
            var list = new List<Song>(Count);
            for (Node t = _head; t != null; t = t.Next)
                list.Add(t.Data);
            return list;
        }
        finally { _rwLock.ExitReadLock(); }
    }


    // Thu thập tất cả các kết quả, sắp xếp tăng dần theo độ lớn.
    // Lấy tập NHỎ NHẤT làm gốc để duyệt. Điều này triệt tiêu hoàn toàn
    // việc copy và quét qua một tập hợp khổng lồ (ví dụ 10,000 bài).
    public List<Song> Find(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return new List<Song>();


        var tokens = keyword.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var tokenResultSets = new List<HashSet<Node>>();


        _rwLock.EnterReadLock();
        try
        {
            // Bước 1: Thu thập tất cả các tập hợp ứng với mỗi token
            foreach (var token in tokens)
            {
                var candidates = _trie.SearchPrefix(token);
                // Early exit: Chỉ cần 1 từ khóa không có kết quả, trả về rỗng ngay lập tức
                if (candidates == null || candidates.Count == 0)
                    return new List<Song>();

                tokenResultSets.Add(candidates);
            }


            // Bước 2: Sắp xếp các tập hợp theo số lượng phần tử TĂNG DẦN
            tokenResultSets.Sort((a, b) => a.Count.CompareTo(b.Count));


            // Bước 3: Copy tập nhỏ nhất (Ít tốn RAM & Thời gian nhất)
            var smallestSet = tokenResultSets[0];
            var finalResult = new HashSet<Node>(smallestSet);


            // Bước 4: Intersect với các tập lớn hơn
            for (int i = 1; i < tokenResultSets.Count; i++)
            {
                finalResult.IntersectWith(tokenResultSets[i]);
                if (finalResult.Count == 0) return new List<Song>();
            }


            return finalResult.Select(n => n.Data).ToList();
        }
        finally { _rwLock.ExitReadLock(); }
    }
    #endregion


    #region Complex Write Operations (Shuffle & Sort)
    public void Shuffle()
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (_head == null || _head.Next == null) return;


            var nodes = new Node[Count];
            Node cur = _head;
            for (int i = 0; i < Count; i++, cur = cur.Next) nodes[i] = cur;


            for (int n = nodes.Length - 1; n > 0; n--)
            {
                int k = _rng.Next(n + 1);
                (nodes[k], nodes[n]) = (nodes[n], nodes[k]);
            }


            _head = nodes[0];
            _head.Prev = null;


            for (int i = 0; i < nodes.Length - 1; i++)
            {
                nodes[i].Next = nodes[i + 1];
                nodes[i + 1].Prev = nodes[i];
            }


            nodes[nodes.Length - 1].Next = null;
            _tail = nodes[nodes.Length - 1];
        }
        finally { _rwLock.ExitWriteLock(); }
    }


    public void Sort()
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (_head == null || _head.Next == null) return;


            string playingId = _current?.Data.ID;
            var dummy = new Node(null) { Next = _head };


            for (int step = 1; step < Count; step *= 2)
            {
                Node cur = dummy.Next;
                Node tail = dummy;


                while (cur != null)
                {
                    Node left = cur;
                    Node right = Split(left, step);
                    cur = Split(right, step);
                    tail = MergeIterative(left, right, tail);
                }
            }


            _head = dummy.Next;
            _head.Prev = null;


            Node temp = _head;
            while (temp.Next != null)
            {
                temp.Next.Prev = temp;
                temp = temp.Next;
            }
            _tail = temp;


            if (playingId != null && _idIndex.TryGetValue(playingId, out Node playing))
                _current = playing;
        }
        finally { _rwLock.ExitWriteLock(); }
    }


    private static Node Split(Node head, int size)
    {
        if (head == null) return null;
        for (int i = 1; head.Next != null && i < size; i++) head = head.Next;
        Node right = head.Next;
        head.Next = null;
        return right;
    }


    private static Node MergeIterative(Node left, Node right, Node tail)
    {
        Node cur = tail;
        while (left != null && right != null)
        {
            int cmp = string.Compare(left.Data.Title, right.Data.Title, StringComparison.OrdinalIgnoreCase);
            if (cmp == 0) cmp = string.Compare(left.Data.Artist, right.Data.Artist, StringComparison.OrdinalIgnoreCase);


            if (cmp <= 0) { cur.Next = left; left = left.Next; }
            else { cur.Next = right; right = right.Next; }


            cur = cur.Next;
        }
        cur.Next = left ?? right;
        while (cur.Next != null) cur = cur.Next;
        return cur;
    }
    #endregion


    // Giải phóng tài nguyên unmanaged của Lock
    public void Dispose()
    {
        _rwLock?.Dispose();
    }
}
}



