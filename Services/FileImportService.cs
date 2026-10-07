using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Melosoul.Services
{
    public class FileImportService
    {
        private readonly PlaylistLoaderService _loaderService;

        public FileImportService(PlaylistLoaderService loaderService)
        {
            _loaderService = loaderService ?? throw new ArgumentNullException(nameof(loaderService));
        }

        public Task<(List<Song> Songs, int DuplicateCount)> ImportAsync(
            IEnumerable<string> files,
            IEnumerable<string> existingPaths)
        {
            var existing = new HashSet<string>(
                existingPaths ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            return _loaderService.CreateSongsAsync(files, existing);
        }
    }
}
