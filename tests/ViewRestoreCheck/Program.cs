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

failed |= Expect("skel 是骨骼文件", Spine.SpineObject.IsPossibleSkeletonFile("hero.skel"));
failed |= Expect("skel.bytes 是骨骼文件", Spine.SpineObject.IsPossibleSkeletonFile(@"C:\a\hero.skel.bytes"));
failed |= Expect("json 骨骼可被列出", Spine.SpineObject.IsPossibleSkeletonFile("hero.json"));
failed |= Expect("atlas 不当成模型", !Spine.SpineObject.IsPossibleSkeletonFile("hero.atlas"));
failed |= Expect("atlas.txt 不当成模型", !Spine.SpineObject.IsPossibleSkeletonFile("hero.atlas.txt"));
failed |= Expect("png 不当成模型", !Spine.SpineObject.IsPossibleSkeletonFile("hero.png"));

var xamlPath = FindUp("SpineViewer", "Views", "MainWindow.xaml");
var xaml = File.ReadAllText(xamlPath);
var listAt = xaml.IndexOf("x:Name=\"_fileBrowserList\"", StringComparison.Ordinal);
var listTag = listAt > 0 ? xaml.Substring(Math.Max(0, listAt - 80), 160) : "";
failed |= Expect("文件栏用 ListBox，条目才能画出来", listTag.Contains("<ListBox", StringComparison.Ordinal));
failed |= Expect("文件栏不再套用会变空白的 ListView 样式", !listTag.Contains("MyListViewBaseStyle", StringComparison.Ordinal));
failed |= Expect("播放渲染文件没有被文件栏改动标记破坏", File.ReadAllText(rendererPath).Contains("new SFML.Graphics.View(liveView)", StringComparison.Ordinal));

var root = Path.Combine(Path.GetTempPath(), "sv-sidebar-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "模型"));
Directory.CreateDirectory(Path.Combine(root, "模型", "内层"));
File.WriteAllText(Path.Combine(root, "模型", "hero.skel"), "skel");
File.WriteAllText(Path.Combine(root, "模型", "hero.atlas"), "atlas");
File.WriteAllText(Path.Combine(root, "模型", "hero.png"), "png");
File.WriteAllText(Path.Combine(root, "note.txt"), "txt");
File.WriteAllText(Path.Combine(root, "pose.json"), "{}");
try
{
    var top = Spine.Utils.SkeletonDirectoryListing.Read(root);
    failed |= Expect("顶层列出中文子文件夹", top.Entries.Any(e => e.IsDirectory && e.Name == "模型"));
    failed |= Expect("顶层列出 json 骨骼", top.Entries.Any(e => !e.IsDirectory && e.Name == "pose.json"));
    failed |= Expect("顶层不列出 txt", !top.Entries.Any(e => e.Name == "note.txt"));
    failed |= Expect("顶层没有错误", top.Error == Spine.Utils.SkeletonDirectoryError.None);

    var nested = Spine.Utils.SkeletonDirectoryListing.Read(Path.Combine(root, "模型"));
    failed |= Expect("子目录仍然列出更深层文件夹", nested.Entries.Any(e => e.IsDirectory && e.Name == "内层"));
    failed |= Expect("子目录列出 skel", nested.Entries.Any(e => e.Name == "hero.skel"));
    failed |= Expect("子目录隐藏 atlas 和 png", !nested.Entries.Any(e => e.Name is "hero.atlas" or "hero.png"));

    var emptyDir = Path.Combine(root, "空");
    Directory.CreateDirectory(emptyDir);
    var empty = Spine.Utils.SkeletonDirectoryListing.Read(emptyDir);
    failed |= Expect("空目录不是错误", empty.Error == Spine.Utils.SkeletonDirectoryError.None && empty.Entries.Count == 0);

    var missing = Spine.Utils.SkeletonDirectoryListing.Read(Path.Combine(root, "不存在"));
    failed |= Expect("不存在的路径有明确错误", missing.Error == Spine.Utils.SkeletonDirectoryError.NotFound);
}
finally
{
    Directory.Delete(root, true);
}

return failed ? 1 : 0;

static string FindRendererSource() => FindUp("SpineViewer", "ViewModels", "Main", "SFMLRendererViewModel.cs");

static string FindUp(params string[] parts)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
        if (File.Exists(candidate))
            return candidate;
        dir = dir.Parent;
    }

    throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
}

static bool Expect(string name, bool ok)
{
    Console.WriteLine(ok ? $"ok  {name}" : $"FAIL {name}");
    return !ok;
}
