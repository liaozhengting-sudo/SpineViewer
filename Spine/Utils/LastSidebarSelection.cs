namespace Spine.Utils;

/// <summary>
/// 只记住本次运行里侧栏最近一次用户选择。可以是一个文件、一个文件夹，或同一次多选的一批。
/// 不写入磁盘，重启后为空。
/// </summary>
public static class LastSidebarSelection
{
    public const string ColorHex = "#8ECAFF";

    /// <summary>
    /// 最近一次选择始终用浅蓝写在正文上，选中和失焦都不收回。
    /// 选中时另有深色底，避免浅蓝字被选中样式盖住或变成黑字。
    /// </summary>
    public static bool UseMarkColor(bool isLastChosen, bool isSelected, bool listHasFocus)
        => isLastChosen;

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
    /// 用户做出非空选择时，用这一批完整替换旧标记。空选择和程序刷新不改已有标记。
    /// </summary>
    public static IReadOnlyList<string> Replace(IReadOnlyList<string> current, IReadOnlyList<string>? chosen, bool fromUser)
    {
        var kept = Normalize(current);
        if (!fromUser || chosen is null || chosen.Count == 0)
            return kept;
        return Normalize(chosen);
    }

    /// <summary>
    /// 旧配置里的访问历史不作为着色来源。
    /// </summary>
    public static IReadOnlyList<string> FromSavedState(string? json) => [];

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
