# Melosoul — Music Playlist Manager

> Ứng dụng quản lý playlist nhạc desktop được xây dựng bằng **C# WinForms**, sử dụng **Doubly Linked List** tự cài đặt làm cấu trúc dữ liệu cốt lõi.

---

## Mục lục

- [Thành viên nhóm](#-thành-viên-nhóm)
- [Báo cáo](#-báo-cáo)
- [Giới thiệu](#-giới-thiệu)
- [Tính năng](#-tính-năng)
- [Áp dụng 4 tính chất OOP](#-áp-dụng-4-tính-chất-oop)
- [Kiến trúc dự án](#-kiến-trúc-dự-án)
- [Cài đặt & Chạy](#-cài-đặt--chạy)
- [Hướng dẫn sử dụng](#-hướng-dẫn-sử-dụng)
- [Công nghệ sử dụng](#-công-nghệ-sử-dụng)
- [Unit Tests](#-unit-tests)
- [Hạn chế hiện tại](#-hạn-chế-hiện-tại)
- [Hướng phát triển](#-hướng-phát-triển-đề-xuất)
- [Ghi chú kỹ thuật](#-ghi-chú-kỹ-thuật)

---

Đại học Kinh tế TP.HCM (UEH)

**Môn học:** Cấu trúc Dữ liệu và Giải thuật

**Học kỳ:** *đầu 2026*

## Thành viên Nhóm

| Họ tên | MSSV |
| --- | --- |
| Tạ Ngọc Bảo Ngân | 31251020415 |
| Nguyễn Trang Nhật Mai | 31251022338 |
| Trần Nguyễn Quỳnh Trâm | 31251024490 |

---
## Báo cáo

**Link dẫn đến báo cáo:** https://docs.google.com/document/d/1vW9ttJHE0tkALVftGr5Wh9BG469NvLfXajuwrj-UxX8/edit?usp=sharing

---

## Giới thiệu

**Melosoul** là ứng dụng nghe nhạc desktop chạy trên Windows, được phát triển trong khuôn khổ môn học **Lập trình hướng đối tượng** tại **Trường Đại học Kinh tế TP.HCM (UEH)**.


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

## Áp dụng 4 tính chất OOP

Dự án Melosoul áp dụng chặt chẽ 4 tính chất cơ bản của Lập trình Hướng đối tượng (OOP) để đảm bảo mã nguồn rõ ràng, dễ bảo trì và an toàn:

### 1. Tính Đóng gói (Encapsulation)
- **Che giấu dữ liệu nội bộ:** Lớp `PlaylistLinkedList` hoàn toàn che giấu cấu trúc `Node` bên trong và cách quản lý các con trỏ `_head`, `_tail`, `_current`. Giao diện UI (`PlayerForm`) chỉ tương tác thông qua các phương thức public an toàn như `AddLast()`, `Next()`, `Prev()`.
- **Đồng bộ hóa (Thread Safety) ẩn:** Cơ chế khóa đa luồng `ReaderWriterLockSlim` được đóng gói hoàn toàn bên trong `PlaylistLinkedList`. Các tiến trình ngầm (Background tasks) và UI thread không bao giờ phải lo lắng về việc xung đột dữ liệu.
- **Custom Controls độc lập:** `CustomVolumeSlider` và `CustomPlaylistScrollBar` tự quản lý toàn bộ logic tính toán tọa độ chuột (`OnMouseMove`) và vẽ đồ họa GDI+ (`OnPaint`), chỉ bộc lộ các Property/Event cơ bản ra bên ngoài.

### 2. Tính Kế thừa (Inheritance)
- **Kiến trúc dữ liệu:** Lớp `Song` (đại diện cho một bài hát) được kế thừa từ lớp cơ sở trừu tượng `MediaFile`. Nhờ đó, `Song` tự động có sẵn các thuộc tính nền tảng như `Id`, `Title`, `FilePath` từ lớp cha mà không cần định nghĩa lại.
- **Tái sử dụng UI Control:** Các custom control kế thừa trực tiếp từ các control có sẵn của .NET WinForms (như `Panel` và `Control`), giúp tái sử dụng toàn bộ nền tảng quản lý sự kiện (event-routing) và handle cửa sổ của hệ điều hành.

### 3. Tính Đa hình (Polymorphism)
- **Ghi đè phương thức (Overriding):** Lớp `Song` ghi đè phương thức abstract `GetDisplayText()` của `MediaFile` để cung cấp cách hiển thị riêng (hiển thị `Title - Artist`). Nếu sau này dự án mở rộng thêm class `VideoFile` kế thừa từ `MediaFile`, nó có thể tự định nghĩa `GetDisplayText()` theo cách khác biệt.
- **Liên kết động (Dynamic Binding):** Bất kỳ phương thức nào nhận tham số kiểu `MediaFile` đều có thể nhận một object kiểu `Song`, và khi gọi `GetDisplayText()`, nó sẽ tự động chạy mã của lớp `Song` ở thời điểm thực thi (runtime).

### 4. Tính Trừu tượng (Abstraction)
- **Giao diện hợp đồng (Interface):** Hệ thống lưu trữ sử dụng Interface `IPlaylistStorage` định nghĩa một hợp đồng trừu tượng gồm 2 hàm cơ bản: `Save()` và `Load()`.
- **Linh hoạt và dễ mở rộng:** Lớp `AutoSaveService` hiện tại triển khai `IPlaylistStorage` để lưu playlist dưới định dạng văn bản (text). Nhờ tính trừu tượng này, sau này có thể dễ dàng tạo thêm `JsonPlaylistStorage` hay `DatabasePlaylistStorage` và cắm (plug) vào ứng dụng mà không làm ảnh hưởng tới các đoạn code đang gọi hàm lưu trữ.

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
O(1) — chỉnh lại 2 con trỏ |
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

`MediaMetadataService` (dùng khi load bài hát mới) sử dụng chiến lược đa nguồn để tính toán thời lượng chính xác, trong khi `DurationService` đóng vai trò là cơ chế fallback nhanh. Quy trình của `MediaMetadataService`:

| Bước | Hành động |
| :---: | :--- |
| 1 | Kiểm tra `ConcurrentDictionary` cache — trả về ngay nếu có |
| 2 | Thử đọc từ TagLibSharp (metadata tag của file) |
| 3 | Thử đọc từ Windows Media Player COM API |
| 4 | Nếu 2 nguồn lệch nhau > 15% → probe bằng cách phát thật (volume = 0) |
| 5 | Chọn kết quả gần với probe nhất, ưu tiên TagLib cho `.mp3` |
| 6 | Lưu vào cache để không xử lý lại |

Cách này giải quyết triệt để trường hợp file MP3 VBR (Variable Bit Rate) mà WMP thường báo sai thời lượng.

### Thiết kế Custom Controls

Cả hai control được vẽ hoàn toàn bằng GDI+ (`OnPaint`), không dùng control mặc định của Windows:

| Control | Đặc điểm |
| :--- | :--- |
| `CustomPlaylistScrollBar` | Track và thumb vẽ bằng `GraphicsPath` góc bo tròn. 3 trạng thái thumb: bình thường / hover / kéo. Chiều cao thumb proportional theo `viewportSize / totalItems`. |
| `CustomVolumeSlider` | Track ngang với phần fill màu từ đầu đến vị trí thumb. Thumb hình tròn với hiệu ứng hover. Hỗ trợ kéo chuột mượt mà. |

---
