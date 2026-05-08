# Melosoul — Music Playlist Manager

> Ứng dụng quản lý playlist nhạc desktop được xây dựng bằng **C# WinForms**, sử dụng **Doubly Linked List** tự cài đặt làm cấu trúc dữ liệu cốt lõi.

---

## Mục lục

- [Thành viên nhóm](#-thành-viên-nhóm)
- [Giới thiệu](#-giới-thiệu)
- [Tính năng](#-tính-năng)
- [Cấu trúc dữ liệu & Thuật toán](#-cấu-trúc-dữ-liệu--thuật-toán)
- [Kiến trúc dự án](#-kiến-trúc-dự-án)
- [Cài đặt & Chạy](#-cài-đặt--chạy)
- [Hướng dẫn sử dụng](#-hướng-dẫn-sử-dụng)
- [Công nghệ sử dụng](#-công-nghệ-sử-dụng)
- [Unit Tests](#-unit-tests)
- [Hạn chế hiện tại](#-hạn-chế-hiện-tại)
- [Hướng phát triển](#-hướng-phát-triển-đề-xuất)
- [Ghi chú kỹ thuật](#-ghi-chú-kỹ-thuật)

---

**Trường:** Đại học Kinh tế TP.HCM (UEH)

**Môn học:** Cấu trúc Dữ liệu và Giải thuật

**Học kỳ:** *đầu 2026*

## Thành viên Nhóm

| Họ tên | MSSV | Vai trò |
| --- | --- | --- |
| Tạ Ngọc Bảo Ngân | 31251020415 | Thiết kế Winforms & Tích hợp code |
| Nguyễn Ngọc Vân Lâm | 31251022996 | Code xử lý chuyển đổi bài hát và viết báo cáo |
| Nguyễn Trang Nhật Mai | 31251022338 | Thiết kế và cài đặt cấu trúc dữ liệu |
| Trần Nguyễn Quỳnh Trâm | 31251024490 | Viết báo cáo, thuyết trình |

---


## Giới thiệu

**Melosoul** là ứng dụng nghe nhạc desktop chạy trên Windows, được phát triển trong khuôn khổ môn học **Cấu trúc Dữ liệu và Giải thuật** tại **Trường Đại học Kinh tế TP.HCM (UEH)**.

Điểm trọng tâm của đề tài là tự cài đặt hoàn toàn cấu trúc **Doubly Linked List** — không sử dụng `LinkedList<T>` có sẵn của .NET — và khai thác đặc tính hai chiều của nó để xây dựng các tính năng phát nhạc tự nhiên: phát tiếp theo, phát bài trước, lặp lại, v.v.

Ngoài phần CTDL bắt buộc, nhóm đã mở rộng thêm nhiều tính năng thực tế như tìm kiếm Trie, sắp xếp Merge Sort, xử lý đa luồng, đọc album art từ ID3 tag, và lưu playlist tự động.

---

## Tính năng

### Phát nhạc

| Tính năng | Mô tả |
| :--- | :--- |
| ▶ Phát / ⏸ Tạm dừng | Toggle bằng nút hoặc phím `Space` |
| ⏭ Bài tiếp theo | Nút Next hoặc phím `→` / `Ctrl+→` |
| ⏮ Bài trước | Nút Prev hoặc phím `←` / `Ctrl+←` |
| 🔁 Lặp toàn bộ (Repeat All) | Vòng lại đầu danh sách khi hết |
| 🔁 Lặp một bài (Repeat One) | Phát lại bài hiện tại khi kết thúc |
| 🔊 Điều chỉnh âm lượng | Thanh trượt tùy chỉnh (CustomVolumeSlider) |
| ⏩ Tua thời gian | Click hoặc kéo thanh tiến trình (drag-to-seek) |

### Quản lý Playlist

| Tính năng | Mô tả |
| :--- | :--- |
| Thêm bài | Qua dialog chọn file hoặc kéo-thả (drag & drop) nhiều file |
| Xóa bài | Chọn bài và nhấn Remove, có xác nhận |
| Reset playlist | Xóa toàn bộ, có xác nhận |
| Đổi tên / sửa nghệ sĩ | Double-click vào bài trong danh sách |
| Sắp xếp | Merge Sort theo tên bài hát (A→Z), phụ theo nghệ sĩ |
| Xáo trộn | Shuffle theo thuật toán Fisher-Yates |
| Tìm kiếm | Tìm kiếm theo prefix, hỗ trợ nhiều từ khóa (AND logic) qua Trie |
| Lưu playlist | Xuất file `.mp3` ra thư mục tùy chọn |
| AutoSave / AutoLoad | Tự động lưu khi đóng, tự động khôi phục khi mở lại |

### Giao diện

- Album art tự động: đọc ảnh nhúng từ ID3 tag (MP3), tìm ảnh bìa trong thư mục, hoặc hiển thị icon nhạc mặc định
- Thanh cuộn playlist tùy chỉnh (`CustomPlaylistScrollBar`)
- Hiển thị thời gian thực: thời điểm hiện tại / tổng thời lượng bài
- Trạng thái bài đang phát được highlight trên danh sách
- Giao diện tối (dark theme) nhất quán

---

## Cấu trúc Dữ liệu & Thuật toán

### 1. Doubly Linked List — `PlaylistLinkedList`

CTDL trọng tâm của toàn bộ dự án, được cài đặt hoàn toàn từ đầu trong `DataStructures/PlaylistLinkedList.cs`.

```
HEAD <-> [Song A] <-> [Song B] <-> [Song C] <-> TAIL
                          ^
                       _current (bai dang phat)
```

Mỗi `Node` lưu: `Data` (Song), `Next` (con trỏ tiếp theo), `Prev` (con trỏ phía trước).

**Bảng độ phức tạp các thao tác:**

| Thao tác | Độ phức tạp | Mô tả |
| :--- | :---: | :--- |
| `AddFirst(song)` | O(1) | Thêm vào đầu danh sách |
| `AddLast(song)` | O(1) | Thêm vào cuối danh sách |
| `Remove(id)` | O(1) | Xóa node bằng ID (tra qua Dictionary) |
| `MoveTo(id)` | O(1) | Di chuyển con trỏ `_current` đến bài chỉ định |
| `Next()` | O(1) | Chuyển sang bài tiếp theo, hỗ trợ Repeat All |
| `Prev()` | O(1) | Chuyển về bài trước, hỗ trợ Repeat All |
| `GetById(id)` | O(1) | Lấy Song theo ID (Read Lock) |
| `UpdateSongMetadata` | O(k) | Cập nhật tên/nghệ sĩ, tự cập nhật Trie index |
| `ToList()` | O(n) | Duyệt và trả về danh sách |
| `Find(keyword)` | O(k + m) | Tìm kiếm multi-token qua Trie |
| `Sort()` | O(n log n) | Merge Sort bottom-up tại chỗ |
| `Shuffle()` | O(n) | Fisher-Yates trên mảng tạm |
| `Clear()` | O(1) | Đặt lại toàn bộ danh sách |

> **Ghi chú:** k = độ dài keyword, m = kích thước tập kết quả nhỏ nhất trong Trie

---

### 2. Trie — Tìm kiếm theo Prefix

Cấu trúc **Trie** nhúng bên trong `PlaylistLinkedList`, hỗ trợ tìm kiếm theo prefix O(k) thay vì O(n).

**Cách hoạt động:**

1. Mỗi bài hát được tokenize thành các từ từ `Title` và `Artist` (lowercase, tách theo khoảng trắng và dấu `-_,.`)
2. Mỗi token được insert vào Trie; mỗi node trên đường đi lưu tham chiếu đến `Node` tương ứng
3. Tìm kiếm nhiều từ khóa trả về **giao** của các tập kết quả — tập nhỏ nhất làm gốc để tối ưu bộ nhớ

```
Vi du: Tim "son tung"
  Trie.SearchPrefix("son")  --> {NodeA, NodeB, NodeC}
  Trie.SearchPrefix("tung") --> {NodeA, NodeD}
  Ket qua = {NodeA}
```

---

### 3. Merge Sort — Bottom-Up, tại chỗ trên Linked List

Cài đặt theo chiến lược **bottom-up iterative** — không đệ quy, không mảng phụ, sort trực tiếp trên con trỏ.

```
Buoc 1 (step=1): Merge tung cap node  [A|B] [C|D] [E|F] ...
Buoc 2 (step=2): Merge tung nhom 4   [ABCD] [EFGH] ...
Buoc 4 (step=4): Merge tung nhom 8   ...
```

Tiêu chí: tên bài hát (A→Z), tiebreak theo tên nghệ sĩ. Con trỏ `_current` được duy trì đúng sau sort qua `_idIndex`.

---

### 4. Fisher-Yates Shuffle

```csharp
var nodes = new Node[Count];
// Thu thap tat ca node vao mang tam, sau do:
for (int n = nodes.Length - 1; n > 0; n--)
{
    int k = _rng.Next(n + 1);
    (nodes[k], nodes[n]) = (nodes[n], nodes[k]);
}
// Ket noi lai con tro Prev/Next
```

---

### 5. Thread Safety — ReaderWriterLockSlim

| Loại Lock | Các thao tác được bảo vệ |
| :---: | :--- |
| **Read Lock** | `CurrentSong`, `ToList()`, `Find()`, `GetById()` — nhiều luồng đọc đồng thời |
| **Write Lock** | `Add`, `Remove`, `MoveTo`, `Next`, `Prev`, `Sort`, `Shuffle`, `UpdateSongMetadata` — chỉ 1 luồng ghi |

---

## Kiến trúc Dự án

```
Melosoul/
├── DataStructures/
│   └── PlaylistLinkedList.cs
├── Models/
│   └── Song.cs
├── Forms/
│   ├── PlayerForm.cs
│   ├── PlayerForm.Playback.cs
│   ├── PlayerForm.Playlist.cs
│   ├── PlayerForm.IO.cs
│   ├── AddSongDialog.cs
│   └── RenameSongDialog.cs
├── Services/
│   ├── AlbumArtService.cs
│   ├── DurationService.cs
│   ├── MediaMetadataService.cs
│   ├── FileImportService.cs
│   ├── PlaylistLoaderService.cs
│   ├── AutoSaveService.cs
│   └── AppText.cs
├── Helpers/
│   ├── CustomPlaylistScrollBar.cs
│   └── CustomVolumeSlider.cs
└── Melosoul.Tests/
    ├── PlaylistLinkedListTests.cs
    └── ServiceTests.cs
```

**Mô tả các thành phần chính:**

| File / Folder | Vai trò |
| :--- | :--- |
| `PlaylistLinkedList.cs` | CTDL Doubly Linked List tự cài đặt (core của toàn dự án) |
| `Song.cs` | Model dữ liệu bài hát (ID, Title, Artist, FilePath) |
| `PlayerForm.cs` | Form chính — khởi tạo, phím tắt, DWM theme |
| `PlayerForm.Playback.cs` | Logic phát nhạc, WMP event, AutoNext, seek |
| `PlayerForm.Playlist.cs` | Render playlist, sort, shuffle, search, scrollbar |
| `PlayerForm.IO.cs` | Thêm/xóa/load file, save playlist ra thư mục |
| `AlbumArtService.cs` | Đọc ID3 tag ảnh, tìm file bìa, sinh ảnh mặc định |
| `DurationService.cs` | Lấy thời lượng bài (WMP + TagLib, có cache) |
| `MediaMetadataService.cs` | Đọc metadata đầy đủ (TagLib + WMP, đối chiếu kết quả) |
| `AutoSaveService.cs` | Lưu/đọc playlist từ AppData (định dạng Base64 v2) |
| `CustomPlaylistScrollBar.cs` | Thanh cuộn playlist vẽ bằng GDI+ |
| `CustomVolumeSlider.cs` | Thanh âm lượng vẽ bằng GDI+ |

### Luồng dữ liệu khi thêm file

```
Nguoi dung chon file
        |
        v
FileImportService.ImportAsync()
        |
        v
PlaylistLoaderService.CreateSongsAsync()   <-- doc metadata bang TagLib
        |
        v
PlaylistLinkedList.AddLast()   <-->  Trie + Dictionary index cap nhat
        |
        v
PlayerForm.RefreshPlaylistUI()
        |
        +---> DurationService.Resolve()    (background Task, co cache)
        |
        +---> AlbumArtService.*            (background Task)
        |
        v
DataGridView cap nhat
```

---

## Cài đặt & Chạy

### Yêu cầu hệ thống

| Yêu cầu | Chi tiết |
| :--- | :--- |
| Hệ điều hành | Windows 10 / 11 |
| Runtime | .NET Framework 4.7.2 trở lên |
| IDE | Visual Studio 2019 hoặc mới hơn |
| Media | Windows Media Player đã cài trên máy |

### Các bước chạy

**1. Clone repository**

```bash
git clone https://github.com/nganta31251020415-afk/Melosoul.git
```

**2. Mở solution**

Mở file `Melosoul.sln` bằng Visual Studio.

**3. Restore NuGet packages**

Visual Studio sẽ tự restore. Packages cần có:

- `TagLibSharp` — đọc metadata audio
- `ComponentFactory.Krypton.Toolkit` — UI theme

**4. Lưu ý về Krypton Toolkit**

`Melosoul.csproj` tham chiếu `ComponentFactory.Krypton.Toolkit.dll` qua `HintPath` local. Nếu máy không có đúng đường dẫn, cần chỉnh lại `HintPath` trỏ tới DLL hợp lệ hoặc thêm thư viện bằng cách tham chiếu trực tiếp DLL trong solution.

**5. Build & Run**

Nhấn `F5` hoặc chọn `Debug > Start Debugging`.

---

## Hướng dẫn Sử dụng

### Thêm nhạc vào playlist

- **Kéo và thả:** Kéo trực tiếp file nhạc từ File Explorer vào vùng danh sách
- **Nút Load:** Mở hộp thoại chọn nhiều file cùng lúc
- **Nút Add:** Mở dialog thêm một bài thủ công

### Điều khiển phát nhạc

| Thao tác | Cách thực hiện |
| :--- | :--- |
| Phát bài cụ thể | Click một lần vào bài trong danh sách |
| Play / Pause | Nút ▶⏸ hoặc phím `Space` |
| Bài tiếp theo | Nút ⏭ hoặc phím `→` / `Ctrl+→` |
| Bài trước | Nút ⏮ hoặc phím `←` / `Ctrl+←` |
| Tua nhạc | Click hoặc kéo trên thanh tiến trình |
| Chỉnh âm lượng | Kéo thanh trượt volume |

### Sắp xếp & Tìm kiếm

| Chức năng | Cách dùng |
| :--- | :--- |
| Sắp xếp A→Z | Nhấn nút **Sort** |
| Xáo trộn ngẫu nhiên | Bật checkbox **Shuffle** |
| Tìm kiếm | Gõ vào ô tìm kiếm — kết quả cập nhật tức thì, hỗ trợ nhiều từ (AND logic) |

### Quản lý playlist

- **Double-click** vào bài → đổi tên / nghệ sĩ
- **Nút Remove** → xóa bài đang chọn (có xác nhận)
- **Nút Save** → xuất file `.mp3` ra thư mục tùy chọn
- Playlist **tự động lưu** khi đóng và **tự động khôi phục** khi mở lại

### Định dạng được hỗ trợ

`.mp3` &nbsp;·&nbsp; `.mp4` &nbsp;·&nbsp; `.wav` &nbsp;·&nbsp; `.wma` &nbsp;·&nbsp; `.aac` &nbsp;·&nbsp; `.flac` &nbsp;·&nbsp; `.m4a`

---

## Công nghệ Sử dụng

| Thành phần | Công nghệ |
| :--- | :--- |
| Ngôn ngữ | C# (.NET Framework 4.7.2) |
| Giao diện | Windows Forms (WinForms) |
| UI Theme | ComponentFactory.Krypton.Toolkit |
| Phát nhạc | Windows Media Player COM (`WMPLib`) |
| Đọc metadata | TagLibSharp (NuGet) |
| Đọc ID3 ảnh | Tự cài đặt (không dùng thư viện ngoài) |
| Đa luồng | `Task`, `CancellationToken`, `ReaderWriterLockSlim` |
| Unit Testing | MSTest (dự án `Melosoul.Tests`) |
| Quản lý gói | NuGet |
| Lưu trữ | Plain text Base64 (`AppData\Roaming\Melosoul\autosave.txt`) |

---

## Unit Tests

Dự án có project test riêng `Melosoul.Tests` với **MSTest**.

### PlaylistLinkedListTests

| Test case | Mô tả |
| :--- | :--- |
| `Remove_CurrentHeadTail_UpdatesPointersAndCount` | Xóa node head/tail/current, kiểm tra con trỏ và Count |
| `NextPrev_RespectRepeatFlag` | Kiểm tra Next/Prev ở cả 2 trạng thái IsRepeatAll |
| `Sort_ByTitleThenArtist_StableForEqualKeys` | Sort đúng thứ tự, tiebreak theo Artist |
| `Shuffle_PreservesAllNodes_NoDuplicates` | Shuffle không mất hoặc trùng node nào |
| `Stress_BasicOperations_5000Songs` | Hiệu năng: 5000 bài — Sort < 3s, Find < 1s |

### ServiceTests

| Test case | Mô tả |
| :--- | :--- |
| `AutoSave_ParseV2Line_ReturnsSong` | Parse đúng định dạng v2 Base64 |
| `FileImport_Deduplicate_Works` | Lọc trùng file, đếm đúng duplicate count |
| `DurationService_Fallback_OnMissingFile` | Trả về `--:--` cho file không tồn tại |

### Chạy tests

```bash
# Trong Visual Studio: Test > Run All Tests  (Ctrl+R, A)

# Qua CLI
dotnet test Melosoul.Tests/Melosoul.Tests.csproj
```

---

## Hạn chế hiện tại

- Build phụ thuộc COM/WMP và DLL Krypton Toolkit local — cần cấu hình `HintPath` đúng trên máy mới.
- **Save playlist** hiện chỉ xuất được file `.mp3`; các định dạng khác (`.wav`, `.flac`, ...) bị bỏ qua.
- Album art chỉ đọc ảnh nhúng từ `.mp3` (ID3); các định dạng khác fallback sang tìm file ảnh cùng thư mục.
- Giao diện chưa hỗ trợ resize linh hoạt — layout tối ưu cho kích thước cố định.

---

## Hướng phát triển đề xuất

- **Chuẩn hóa dependency:** Đưa Krypton Toolkit lên NuGet, bỏ `HintPath` local để build portable hơn.
- **Mở rộng Save playlist:** Hỗ trợ xuất toàn bộ định dạng audio thay vì chỉ `.mp3`.
- **Import/Export chuẩn:** Hỗ trợ định dạng M3U / JSON để tương thích với các player khác.
- **Lazy loading thumbnail:** Chỉ render ảnh khi row xuất hiện màn hình, tối ưu với playlist lớn.
- **Equalizer / visualizer:** Tích hợp hiệu ứng âm thanh hoặc visualizer sóng âm.
- **Album art cho FLAC/M4A:** Mở rộng `AlbumArtService` đọc tag ảnh từ container không phải MP3.

---

## Ghi chú kỹ thuật

### Tại sao dùng Doubly Linked List thay vì `List<T>`?

| Tiêu chí | `List<T>` | Doubly Linked List |
| :--- | :--- | :--- |
| Chuyển bài kế / trước | O(1) với index, nhưng cần lưu index riêng | O(1) tự nhiên qua `Next` / `Prev` |
| Xóa bài đang phát | O(n) — phải shift các phần tử | O(1) — chỉnh lại 2 con trỏ |
| Repeat / vòng lặp | Phải kiểm tra boundary thủ công | Cấu trúc tự nhiên hỗ trợ |
| Insert đầu danh sách | O(n) | O(1) |

### Tại sao `_idIndex` (Dictionary) tồn tại song song với LinkedList?

`Dictionary<string, Node>` cho phép tra cứu node theo ID trong O(1) — cần thiết cho `Remove(id)`, `MoveTo(id)`, `GetById(id)`, và duy trì đúng `_current` sau khi `Sort()`.

### AutoSave — Định dạng lưu trữ v2

Playlist được tự động lưu tại `%AppData%\Melosoul\autosave.txt`, mỗi dòng:

```
v2|<Base64(Title)>|<Base64(Artist)>|<Base64(FilePath)>
```

Dùng Base64 để đảm bảo ký tự đặc biệt (tiếng Việt, dấu `|`) không gây lỗi khi parse. `AutoSaveService` tự nhận biết định dạng `v2` và backward-compatible với format cũ.

### Xử lý thời lượng bài hát — Duration Resolution

`DurationService` và `MediaMetadataService` dùng chiến lược đa nguồn có cache:

| Bước | Hành động |
| :---: | :--- |
| 1 | Kiểm tra `ConcurrentDictionary` cache — trả về ngay nếu có |
| 2 | Thử đọc từ TagLibSharp (metadata tag của file) |
| 3 | Thử đọc từ Windows Media Player COM API |
| 4 | Nếu 2 nguồn lệch nhau > 15% → probe bằng cách phát thật (volume = 0) |
| 5 | Chọn kết quả gần với probe nhất, ưu tiên TagLib cho `.mp3` |
| 6 | Lưu vào cache để không xử lý lại |

Cách này giải quyết trường hợp file MP3 VBR (Variable Bit Rate) mà WMP thường báo sai thời lượng.

### Thiết kế Custom Controls

Cả hai control được vẽ hoàn toàn bằng GDI+ (`OnPaint`), không dùng control mặc định của Windows:

| Control | Đặc điểm |
| :--- | :--- |
| `CustomPlaylistScrollBar` | Track và thumb vẽ bằng `GraphicsPath` góc bo tròn. 3 trạng thái thumb: bình thường / hover / kéo. Chiều cao thumb proportional theo `viewportSize / totalItems`. |
| `CustomVolumeSlider` | Track ngang với phần fill màu từ đầu đến vị trí thumb. Thumb hình tròn với hiệu ứng hover. Hỗ trợ kéo chuột mượt mà. |

---
