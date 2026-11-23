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

        public const int DefaultMaxDepth = 8;
        public const int DefaultMaxVisited = 20000;

        /// <summary>
        /// 把拖进来的文件和文件夹解析成现有加载器能打开的骨骼路径。
        /// 后缀只来自 <see cref="SpineObject.PossibleSuffixMapping"/>。
        /// 同一目录、同一主名的多种格式只保留一个，避免 json/skel 成对时加载两遍。
        /// </summary>
        public static IReadOnlyList<string> ResolveLoadPaths(
            IEnumerable<string> paths,
            CancellationToken cancellationToken = default,
            int maxDepth = DefaultMaxDepth,
            int maxVisited = DefaultMaxVisited)
        {
            var found = new List<Candidate>();
            var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = 0;

            foreach (var raw in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                if (File.Exists(raw))
                    TryAddFile(raw, found);
                else if (Directory.Exists(raw))
                    Walk(raw, 0);
                if (visited > maxVisited)
                    break;
            }

            return Dedupe(found);

            void Walk(string directory, int depth)
            {
                if (depth > maxDepth || visited > maxVisited)
                    return;
                if (!seenDirectories.Add(directory))
                    return;

                IEnumerable<string> files;
                IEnumerable<string> directories;
                try
                {
                    files = Directory.EnumerateFiles(directory);
                    directories = depth < maxDepth ? Directory.EnumerateDirectories(directory) : [];
                }
                catch (Exception)
                {
                    return;
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    visited++;
                    TryAddFile(file, found);
                    if (visited > maxVisited)
                        return;
                }
                foreach (var child in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Walk(child, depth + 1);
                    if (visited > maxVisited)
                        return;
                }
            }
        }

        private readonly struct Candidate
        {
            public Candidate(string path, string stem, string suffix)
            {
                Path = path;
                Stem = stem;
                Suffix = suffix;
            }

            public string Path { get; }
            public string Stem { get; }
            public string Suffix { get; }
        }

        /// <summary>
        /// 命中 <see cref="SpineObject.PossibleSuffixMapping"/> 里最长的后缀。
        /// </summary>
        public static string? MatchSkeletonSuffix(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            string? best = null;
            foreach (var suffix in SpineObject.PossibleSuffixMapping.Keys)
            {
                if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (best is null || suffix.Length > best.Length)
                    best = suffix;
            }
            return best;
        }

        /// <summary>
        /// 点击已选中的项且没有 Ctrl/Shift 时，应先保住当前多选，方便把它们一起拖走。
        /// </summary>
        public static bool ShouldKeepExistingSelection(bool control, bool shift, bool clickedAlreadySelected)
            => !control && !shift && clickedAlreadySelected;

        private static void TryAddFile(string file, List<Candidate> found)
        {
            var suffix = MatchSkeletonSuffix(file);
            if (suffix is null)
                return;
            var stem = file.Substring(0, file.Length - suffix.Length);
            found.Add(new Candidate(file, stem, suffix));
        }

        private static IReadOnlyList<string> Dedupe(List<Candidate> found)
        {
            return found
                .GroupBy(c => c.Stem, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(c => c.Suffix.Length)
                    .ThenBy(c => c.Suffix.Equals(".json", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .First()
                    .Path)
                .ToArray();
        }
    }
}
