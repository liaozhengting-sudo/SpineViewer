using System.Text.Json;

namespace Spine.Utils;

/// <summary>
/// 只在程序里记住成功进入过的文件夹。不改磁盘属性，也不扫描全盘。
/// </summary>
public static class VisitedDirectories
{
    public const int DefaultCapacity = 200;

    /// <summary>
    /// 暗色表面上和白色正文分开的浅蓝色。选中时不用它，以免盖住选中文字的颜色。
    /// </summary>
    public const string VisitedColorHex = "#8ECAFF";

    public static bool UseVisitedColor(bool isDirectory, bool isVisited, bool isSelected)
        => isDirectory && isVisited && !isSelected;

    public static string? Canonical(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            var normalized = path.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalized));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 只有成功时才加入。失败不会写入，也不会把已有记录清掉。父目录不会被自动补上。
    /// </summary>
    public static IReadOnlyList<string> Remember(IReadOnlyList<string> existing, string? path, bool succeeded, int capacity = DefaultCapacity)
    {
        var current = Normalize(existing);
        if (!succeeded || capacity < 1)
            return current;
        var canonical = Canonical(path);
        if (canonical is null)
            return current;

        var result = new List<string> { canonical };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { canonical };
        foreach (var item in current)
        {
            if (seen.Add(item))
                result.Add(item);
        }
        if (result.Count > capacity)
            result.RemoveRange(capacity, result.Count - capacity);
        return result;
    }

    /// <summary>
    /// 只标记这次被拖入或选中、并且里面确实加载出模型的文件夹。
    /// </summary>
    public static IReadOnlyList<string> RememberLoadedFolders(
        IReadOnlyList<string> existing,
        IEnumerable<string> droppedDirectories,
        IEnumerable<string> loadedFiles,
        int capacity = DefaultCapacity)
    {
        var files = loadedFiles.Select(Canonical).Where(path => path is not null).Cast<string>().ToArray();
        var next = Normalize(existing);
        foreach (var dropped in droppedDirectories)
        {
            var dir = Canonical(dropped);
            if (dir is null)
                continue;
            var prefix = dir + Path.DirectorySeparatorChar;
            var hit = files.Any(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!hit)
                continue;
            next = Remember(next, dir, true, capacity);
        }
        return next;
    }

    public static bool Contains(IReadOnlyList<string> existing, string? path)
    {
        var canonical = Canonical(path);
        if (canonical is null)
            return false;
        return Normalize(existing).Any(item => string.Equals(item, canonical, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> ReadFromUserStateJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("VisitedDirectories", out var array) || array.ValueKind != JsonValueKind.Array)
                return [];
            return Normalize(array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? ""));
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> paths)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var canonical = Canonical(path);
            if (canonical is null || !seen.Add(canonical))
                continue;
            result.Add(canonical);
        }
        return result;
    }
}
