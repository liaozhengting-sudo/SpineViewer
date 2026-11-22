using System.IO;

namespace Spine.Utils
{
    public enum SkeletonDirectoryError
    {
        None = 0,
        NotFound,
        AccessDenied,
        Failed,
    }

    public sealed class SkeletonDirectoryEntry
    {
        public SkeletonDirectoryEntry(string name, string fullPath, bool isDirectory)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
        }

        public string Name { get; }
        public string FullPath { get; }
        public bool IsDirectory { get; }
    }

    /// <summary>
    /// 只读当前这一层。子文件夹始终保留；文件只保留可加载的骨骼文件。
    /// </summary>
    public sealed class SkeletonDirectoryListing
    {
        public SkeletonDirectoryListing(IReadOnlyList<SkeletonDirectoryEntry> entries, SkeletonDirectoryError error, string? errorDetail)
        {
            Entries = entries;
            Error = error;
            ErrorDetail = errorDetail;
        }

        public IReadOnlyList<SkeletonDirectoryEntry> Entries { get; }
        public SkeletonDirectoryError Error { get; }
        public string? ErrorDetail { get; }
        public int FolderCount => Entries.Count(e => e.IsDirectory);
        public int SkeletonCount => Entries.Count(e => !e.IsDirectory);

        public static SkeletonDirectoryListing Read(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new([], SkeletonDirectoryError.NotFound, path);

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                return new([], SkeletonDirectoryError.Failed, ex.Message);
            }

            if (!Directory.Exists(fullPath))
                return new([], SkeletonDirectoryError.NotFound, fullPath);

            var entries = new List<SkeletonDirectoryEntry>();
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(fullPath))
                    entries.Add(new SkeletonDirectoryEntry(Path.GetFileName(dir), dir, true));
                foreach (var file in Directory.EnumerateFiles(fullPath))
                {
                    if (SpineObject.IsPossibleSkeletonFile(file))
                        entries.Add(new SkeletonDirectoryEntry(Path.GetFileName(file), file, false));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Finish(entries, SkeletonDirectoryError.AccessDenied, ex.Message);
            }
            catch (Exception ex)
            {
                return Finish(entries, SkeletonDirectoryError.Failed, ex.Message);
            }

            return Finish(entries, SkeletonDirectoryError.None, null);
        }

        private static SkeletonDirectoryListing Finish(List<SkeletonDirectoryEntry> entries, SkeletonDirectoryError error, string? detail)
        {
            entries.Sort((a, b) =>
            {
                var byKind = b.IsDirectory.CompareTo(a.IsDirectory);
                return byKind != 0 ? byKind : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return new(entries, error, detail);
        }
    }
}
