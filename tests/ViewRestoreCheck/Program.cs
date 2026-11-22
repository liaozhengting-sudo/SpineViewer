using SFML.Graphics;
using SFML.System;

// 不创建窗口。验证两件事：
// 1. View 复制能保住负的 Size.Y（垂直翻转）和缩放后的尺寸，源视区被改写后副本不变。
//    SFML GetView 返回的就是会被 SetView 改写的内部别名。
// 2. 棋盘格绘制先复制视区，再把副本设回去。

var failed = false;
using var source = new View(new Vector2f(10, 20), new Vector2f(80, -40));
using var copy = new View(source);
source.Size = new Vector2f(200, 100);
source.Center = new Vector2f(0, 0);

failed |= Expect("副本保住缩放宽度", Math.Abs(copy.Size.X - 80f) < 0.01f);
failed |= Expect("副本保住负的高度，模型不会上下颠倒", copy.Size.Y < 0 && Math.Abs(copy.Size.Y - (-40f)) < 0.01f);
failed |= Expect("副本中心不被源视区改写", Math.Abs(copy.Center.X - 10f) < 0.01f && Math.Abs(copy.Center.Y - 20f) < 0.01f);

var rendererPath = FindRendererSource();
var renderer = File.ReadAllText(rendererPath);
failed |= Expect("棋盘格绘制先复制视区", renderer.Contains("new SFML.Graphics.View(liveView)", StringComparison.Ordinal));
failed |= Expect(
    "恢复时用的是副本而不是 GetView 别名",
    renderer.Contains("renderer.SetView(worldView)", StringComparison.Ordinal)
    && !renderer.Contains("renderer.SetView(liveView)", StringComparison.Ordinal));

return failed ? 1 : 0;

static string FindRendererSource()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "SpineViewer", "ViewModels", "Main", "SFMLRendererViewModel.cs");
        if (File.Exists(candidate))
            return candidate;
        dir = dir.Parent;
    }

    throw new FileNotFoundException("SFMLRendererViewModel.cs");
}

static bool Expect(string name, bool ok)
{
    Console.WriteLine(ok ? $"ok  {name}" : $"FAIL {name}");
    return !ok;
}
