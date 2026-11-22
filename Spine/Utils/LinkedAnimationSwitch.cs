namespace Spine.Utils;

/// <summary>
/// 多个模型之间按动画名完全一致做一次切换。
/// 只处理调用方传入的模型，名称相近但不相等的不会配在一起。
/// </summary>
public static class LinkedAnimationSwitch
{
    /// <summary>
    /// 预览进度条使用的轨道。联动只改这一条，不清理其它轨道。
    /// </summary>
    public const int PrimaryTrack = 0;

    public readonly record struct ModelAnimations(string Name, IReadOnlyList<string> Animations);

    public readonly record struct ApplyPlan(
        string AnimationName,
        int TrackIndex,
        bool[] Apply,
        string[] AppliedNames,
        string[] SkippedNames);

    /// <summary>
    /// 只留下正在显示的模型。隐藏的既不列出，也不会被切换。
    /// </summary>
    public static ModelAnimations[] VisibleTargets(IReadOnlyList<(string Name, bool Shown, IReadOnlyList<string> Animations)> loaded)
    {
        return loaded
            .Where(model => model.Shown)
            .Select(model => new ModelAnimations(model.Name, model.Animations))
            .ToArray();
    }

    /// <summary>
    /// 至少两个模型都有的动画名，区分大小写，保留第一次出现的顺序。
    /// 每个模型里的重复名字只计一次。
    /// </summary>
    public static string[] ExactSharedNames(IReadOnlyList<IReadOnlyList<string>> modelAnimations)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var model in modelAnimations)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in model)
            {
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    continue;
                if (counts.TryGetValue(name, out var count))
                    counts[name] = count + 1;
                else
                {
                    counts[name] = 1;
                    order.Add(name);
                }
            }
        }

        return order.Where(name => counts[name] >= 2).ToArray();
    }

    public static ApplyPlan Plan(IReadOnlyList<ModelAnimations> models, string animationName)
    {
        var apply = new bool[models.Count];
        var applied = new List<string>();
        var skipped = new List<string>();
        for (var i = 0; i < models.Count; i++)
        {
            var hasName = models[i].Animations.Contains(animationName, StringComparer.Ordinal);
            apply[i] = hasName;
            if (hasName)
                applied.Add(models[i].Name);
            else
                skipped.Add(models[i].Name);
        }

        return new ApplyPlan(animationName, PrimaryTrack, apply, applied.ToArray(), skipped.ToArray());
    }

    /// <summary>
    /// 对拥有该名字的模型各调用一次设置。回调收到的是模型下标、轨道和动画名，不传递动画实例。
    /// </summary>
    public static ApplyPlan Apply(
        IReadOnlyList<ModelAnimations> models,
        string animationName,
        Action<int, int, string> setTrackAnimation)
    {
        var plan = Plan(models, animationName);
        for (var i = 0; i < models.Count; i++)
        {
            if (!plan.Apply[i])
                continue;
            setTrackAnimation(i, plan.TrackIndex, animationName);
        }
        return plan;
    }
}
