using System.Reflection;

namespace Spine.Utils;

/// <summary>
/// 换掉某一条轨道上的动画时，切断从上一个动画过渡过来的混合。
/// 不清除其它轨道。
/// </summary>
public static class TrackMixCut
{
    public static void DetachOutgoing(object? trackEntry)
    {
        var current = Unwrap(trackEntry);
        if (current is null)
            return;
        var type = current.GetType();
        foreach (var name in new[] { "previous", "mixingFrom" })
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is null || field.FieldType.IsValueType)
                continue;
            field.SetValue(current, null);
        }
    }

    private static object? Unwrap(object? trackEntry)
    {
        var current = trackEntry;
        for (var i = 0; i < 4 && current is not null; i++)
        {
            var inner = current.GetType().GetProperty("InnerObject", BindingFlags.Instance | BindingFlags.Public)?.GetValue(current);
            if (inner is null || ReferenceEquals(inner, current))
                break;
            current = inner;
        }
        return current;
    }
}
