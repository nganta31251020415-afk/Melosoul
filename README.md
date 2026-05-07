# Melosoul

## Build

Official build path for this repo:

```powershell
dotnet build Melosoul.csproj
```

## Test

```powershell
dotnet test Melosoul.Tests\Melosoul.Tests.csproj
```

## Notes

- `PlayerForm.cs` uses service-first flow for metadata/duration/import:
  - `MediaMetadataService`: single place to create `Song`.
  - `DurationService`: single place to resolve duration + cache.
  - `FileImportService`: deduplicate + import orchestration.
- Legacy temporary build folder is ignored via `.gitignore` (`tempbuild/`).

## Technical Notes

### Why Doubly LinkedList

- Playlist navigation is naturally sequential (`prev/next`) and maps directly to a doubly linked structure.
- `Next` and `Prev` are O(1) from the current node.
- Deleting current song is O(1) once node is found.

### Big-O (main operations)

- `AddFirst`, `AddLast`: O(1)
- `Next`, `Prev`: O(1)
- `MoveTo(id)`: O(1) average (via dictionary index)
- `Remove(id)`: O(1) average (dictionary index + pointer relink)
- `Find(keyword)`: depends on trie candidate sets, typically much better than linear scan
- `Sort`: O(n log n) (linked-list merge sort)
- `Shuffle`: O(n)

### Design Trade-offs

- Added dictionary index by `id` to avoid O(n) lookup before remove/move.
- Added trie for prefix search to improve UX on large playlists.
- Kept WinForms UI thin where possible, moved metadata/duration/import logic into services.
- Chose Windows Media Player interop for playback compatibility with WinForms on Windows.

## Stress Demo

Use this quick benchmark flow:

1. Load 5,000+ local audio files (mixed extensions).
2. Observe `Load: x.xx giây` in status bar.
3. Run search with short prefixes (`a`, `lo`, `thu`) and compare response time.
4. Run `Sort` and `Shuffle` and verify no missing/duplicate songs.

Automated service-level tests are in `Melosoul.Tests` and run with:

```powershell
dotnet test Melosoul.Tests\Melosoul.Tests.csproj
```
