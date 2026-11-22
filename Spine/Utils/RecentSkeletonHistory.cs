using System.Text.Json;

namespace Spine.Utils;

/// <summary>
/// 只记录成功打开过的骨骼文件路径。不扫描磁盘，也不在启动时加载这些文件。
/// </summary>
public static class RecentSkeletonHistory
{
    public const int DefaultCapacity = 30;

    public static bool ShouldRemember(string? path)
        => !string.IsNullOrWhiteSpace(path) && SpineObject.IsPossibleSkeletonFile(path);

    /// <summary>
    /// 新成功的路径放在最前。同一路径只留一条。不是骨骼文件的路径会被丢掉。
    /// </summary>
    public static IReadOnlyList<string> Remember(IReadOnlyList<string> existing, IEnumerable<string> loaded, int capacity = DefaultCapacity)
    {
        if (capacity < 1)
            capacity = DefaultCapacity;

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMany(loaded);
        AddMany(existing);
        if (result.Count > capacity)
            result.RemoveRange(capacity, result.Count - capacity);
        return result;

        void AddMany(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!ShouldRemember(path))
                    continue;
                string full;
                try
                {
                    full = Path.GetFullPath(path);
                }
                catch
                {
                    continue;
                }
                if (seen.Add(full))
                    result.Add(full);
            }
        }
    }

    public static string Label(string path, IReadOnlyCollection<string> all)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            name = path;
        var sameName = 0;
        foreach (var other in all)
        {
            if (string.Equals(Path.GetFileName(other), name, StringComparison.OrdinalIgnoreCase))
                sameName++;
        }
        if (sameName < 2)
            return name;
        var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        return string.IsNullOrEmpty(parent) ? name : name + "  ·  " + parent;
    }

    /// <summary>
    /// 路径不存在或无法访问时返回 true。不抛出。
    /// </summary>
    public static bool IsMissing(string path)
    {
        try
        {
            return !File.Exists(path);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 旧的用户状态没有这份列表时返回空，不要求其它字段。坏掉的文本也返回空。
    /// </summary>
    public static IReadOnlyList<string> ReadFromUserStateJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("RecentOpenedFiles", out var array) || array.ValueKind != JsonValueKind.Array)
                return [];
            var paths = new List<string>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    paths.Add(value);
            }
            return paths;
        }
        catch
        {
            return [];
        }
    }
}
