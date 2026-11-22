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

var suffixes = Spine.SpineObject.PossibleSuffixMapping.Keys.OrderBy(s => s).ToArray();
Console.WriteLine("supported-skeleton-suffixes: " + string.Join(", ", suffixes));
var formatRoot = Path.Combine(Path.GetTempPath(), "sv-fmt-" + Guid.NewGuid().ToString("N"));
var modelDir = Path.Combine(formatRoot, "角色");
Directory.CreateDirectory(modelDir);
try
{
    var expected = new List<string>();
    foreach (var suffix in suffixes)
    {
        var file = Path.Combine(modelDir, "solo-" + suffix.TrimStart('.').Replace('.', '_') + suffix);
        File.WriteAllText(file, "x");
        expected.Add(Path.GetFullPath(file));
    }
    File.WriteAllText(Path.Combine(modelDir, "hero.atlas"), "atlas");
    File.WriteAllText(Path.Combine(modelDir, "hero.png"), "png");
    File.WriteAllText(Path.Combine(modelDir, "hero.atlas.txt"), "atlas");
    foreach (var suffix in suffixes)
        File.WriteAllText(Path.Combine(modelDir, "same" + suffix), "x");

    var discovered = Spine.Utils.SkeletonDirectoryListing.ResolveLoadPaths([formatRoot]);
    foreach (var file in expected)
        failed |= Expect("文件夹发现支持格式 " + Path.GetFileName(file), discovered.Contains(file, StringComparer.OrdinalIgnoreCase));
    failed |= Expect("不把 atlas/png 当成模型", !discovered.Any(p => p.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".atlas.txt", StringComparison.OrdinalIgnoreCase)));
    var same = discovered.Where(p => Path.GetFileName(p).StartsWith("same", StringComparison.OrdinalIgnoreCase)).ToArray();
    failed |= Expect("同一主名的多种骨骼格式只保留一个", same.Length == 1);
    var direct = Path.Combine(formatRoot, "only" + suffixes[0]);
    File.WriteAllText(direct, "x");
    var fromFile = Spine.Utils.SkeletonDirectoryListing.ResolveLoadPaths([direct]);
    failed |= Expect("直接拖入支持的文件仍会加载", fromFile.Count == 1);

    var mixA = Path.Combine(formatRoot, "甲");
    var mixB = Path.Combine(formatRoot, "乙");
    Directory.CreateDirectory(mixA);
    Directory.CreateDirectory(mixB);
    File.WriteAllText(Path.Combine(mixA, "a.json"), "{}");
    File.WriteAllText(Path.Combine(mixA, "a.atlas"), "atlas");
    File.WriteAllText(Path.Combine(mixB, "b.skel"), "skel");
    File.WriteAllText(Path.Combine(mixB, "b.skel.bytes"), "bytes");
    var loose = Path.Combine(formatRoot, "loose.skel.bytes");
    File.WriteAllText(loose, "bytes");
    var mixed = Spine.Utils.SkeletonDirectoryListing.ResolveLoadPaths([mixA, mixB, loose, Path.Combine(mixA, "a.png")]);
    failed |= Expect("混合拖入包含甲的 json", mixed.Any(p => p.EndsWith(Path.Combine("甲", "a.json"), StringComparison.OrdinalIgnoreCase)));
    failed |= Expect("混合拖入乙只保留一个成对格式", mixed.Count(p => p.Contains($"{Path.DirectorySeparatorChar}乙{Path.DirectorySeparatorChar}")) == 1);
    failed |= Expect("混合拖入包含单独的 skel.bytes", mixed.Any(p => p.EndsWith("loose.skel.bytes", StringComparison.OrdinalIgnoreCase)));
    failed |= Expect("混合拖入不包含不存在的 png", !mixed.Any(p => p.EndsWith("a.png", StringComparison.OrdinalIgnoreCase)));
    failed |= Expect("多选已选项时保留选择", Spine.Utils.SkeletonDirectoryListing.ShouldKeepExistingSelection(false, false, true));
    failed |= Expect("Ctrl 点击不冻结选择", !Spine.Utils.SkeletonDirectoryListing.ShouldKeepExistingSelection(true, false, true));
    failed |= Expect("Shift 点击不冻结选择", !Spine.Utils.SkeletonDirectoryListing.ShouldKeepExistingSelection(false, true, true));
    failed |= Expect("点未选项不冻结选择", !Spine.Utils.SkeletonDirectoryListing.ShouldKeepExistingSelection(false, false, false));

var shared = new[] { "click5_2", "click6", "click11_1", "click11_2", "click12_2", "idle3", "idle4" };
var cinnabarA = shared.Concat(["only_a"]).ToArray();
var cinnabarB = shared.ToArray();
var cinnabarC = shared.Where(name => name != "click12_2").Append("only_c").ToArray();
var names = Spine.Utils.LinkedAnimationSwitch.ExactSharedNames([cinnabarA, cinnabarB, cinnabarC]);
failed |= Expect("三个模型的同名动画放在一起", names.Contains("click6") && names.Contains("idle3") && names.Contains("click11_1"));
failed |= Expect("只存在于一个模型的名字不进联动", !names.Contains("only_a") && !names.Contains("only_c"));
failed |= Expect("相近但不同的名字不会并成一个", names.Contains("click11_1") && names.Contains("click11_2") && !names.Contains("click11"));
failed |= Expect("不是每个模型都有的名字仍可联动其余模型", names.Contains("click12_2"));
failed |= Expect("大小写不同不算同一个动画", Spine.Utils.LinkedAnimationSwitch.ExactSharedNames([["Idle3"], ["idle3"]]).Length == 0);
failed |= Expect("只选一个模型时没有联动项", Spine.Utils.LinkedAnimationSwitch.ExactSharedNames([shared]).Length == 0);
failed |= Expect("名字相近但各自只有一个模型时不联动", Spine.Utils.LinkedAnimationSwitch.ExactSharedNames([["click11_1"], ["click11_2"]]).Length == 0);

var selected = new Spine.Utils.LinkedAnimationSwitch.ModelAnimations[]
{
    new("10070_skin_Cinnabar03a", cinnabarA),
    new("10070_skin_Cinnabar03b", cinnabarB),
    new("10070_skin_Cinnabar03c", cinnabarC),
};
var calls = new List<(int index, int track, string animation)>();
var applied = Spine.Utils.LinkedAnimationSwitch.Apply(selected, "click6", (index, track, animation) => calls.Add((index, track, animation)));
failed |= Expect("同名动画只改轨道 0", applied.TrackIndex == Spine.Utils.LinkedAnimationSwitch.PrimaryTrack && calls.All(call => call.track == 0 && call.animation == "click6"));
failed |= Expect("三个模型各自切一次，不复用同一次调用", calls.Count == 3 && calls.Select(call => call.index).Distinct().Count() == 3);
failed |= Expect("切换 click6 没有跳过模型", applied.SkippedNames.Length == 0 && applied.AppliedNames.Length == 3);

calls.Clear();
var partial = Spine.Utils.LinkedAnimationSwitch.Apply(selected, "click12_2", (index, track, animation) => calls.Add((index, track, animation)));
failed |= Expect("没有该动画的模型被跳过", calls.Count == 2 && !calls.Any(call => call.index == 2));
failed |= Expect("跳过反馈点名缺少动画的模型", partial.SkippedNames.SequenceEqual(["10070_skin_Cinnabar03c"]));
failed |= Expect("跳过时已切换的模型仍然点名", partial.AppliedNames.SequenceEqual(["10070_skin_Cinnabar03a", "10070_skin_Cinnabar03b"]));

calls.Clear();
var none = Spine.Utils.LinkedAnimationSwitch.Apply(selected, "not-a-real-animation", (index, track, animation) => calls.Add((index, track, animation)));
failed |= Expect("谁都没有的动画不会切换", calls.Count == 0 && none.AppliedNames.Length == 0 && none.SkippedNames.Length == 3);

calls.Clear();
var onlyTwo = selected.Take(2).ToArray();
var limited = Spine.Utils.LinkedAnimationSwitch.Apply(onlyTwo, "click6", (index, track, animation) => calls.Add((index, track, animation)));
failed |= Expect("未传入的模型不会被切换", calls.Count == 2 && !limited.AppliedNames.Contains("10070_skin_Cinnabar03c") && !limited.SkippedNames.Contains("10070_skin_Cinnabar03c"));

var shown = Spine.Utils.LinkedAnimationSwitch.VisibleTargets(
[
    ("10070_skin_Cinnabar03a", true, new[] { "click6", "click12_2", "idle3", "click11_1" }),
    ("10070_skin_Cinnabar03b", true, new[] { "click6", "idle3", "click11_2" }),
    ("10070_skin_Cinnabar03c", false, new[] { "click6", "click12_2", "idle3", "secret" }),
]);
failed |= Expect("隐藏模型不进入自动联动", shown.Length == 2 && shown.All(model => model.Name != "10070_skin_Cinnabar03c"));
var shownNames = Spine.Utils.LinkedAnimationSwitch.ExactSharedNames(shown.Select(model => model.Animations).ToArray());
failed |= Expect("显示中的同名动画不用点选也会列出", shownNames.Contains("click6") && shownNames.Contains("idle3"));
failed |= Expect("只和隐藏模型同名的动画不会列出", !shownNames.Contains("click12_2") && !shownNames.Contains("secret"));
failed |= Expect("显示中的相近名字仍然分开", shownNames.Contains("click11_1") == false && shownNames.Contains("click11_2") == false);
calls.Clear();
var shownApply = Spine.Utils.LinkedAnimationSwitch.Apply(shown, "click6", (index, track, animation) => calls.Add((index, track, animation)));
failed |= Expect(
    "自动联动不切换隐藏模型",
    calls.Count == 2
    && calls.All(call => call.track == 0 && call.animation == "click6")
    && !shownApply.AppliedNames.Contains("10070_skin_Cinnabar03c")
    && !shownApply.SkippedNames.Contains("10070_skin_Cinnabar03c"));

var tab = File.ReadAllText(FindUp("SpineViewer", "ViewModels", "Main", "SpineObjectTabViewModel.cs"));
var applyAt = tab.IndexOf("private void ApplyLinkedAnimation", StringComparison.Ordinal);
var applyBody = applyAt >= 0 ? tab.Substring(applyAt, Math.Min(900, tab.Length - applyAt)) : "";
failed |= Expect("联动通过各模型自己的 SetAnimation 设置", applyBody.Contains("SetAnimation(track, animation)", StringComparison.Ordinal));
failed |= Expect("联动切换不会清理轨道", !applyBody.Contains("ClearTrack", StringComparison.Ordinal));
failed |= Expect("动画页保留原来的单模型轨道下拉", xaml.Contains("SelectedValue=\"{Binding AnimationName}\"", StringComparison.Ordinal));
failed |= Expect("动画页有联动下拉", xaml.Contains("LinkedAnimationPick", StringComparison.Ordinal));
var selectedAt = tab.IndexOf("public SpineObjectModel[] SelectedObjects", StringComparison.Ordinal);
var pickAt = tab.IndexOf("private string? _linkedAnimationPick", StringComparison.Ordinal);
var selectedBody = selectedAt >= 0 && pickAt > selectedAt ? tab.Substring(selectedAt, pickAt - selectedAt) : "";
failed |= Expect("点选模型不再重算联动", !selectedBody.Contains("RefreshLinkedAnimations", StringComparison.Ordinal));
failed |= Expect("联动目标是正在显示的模型", applyBody.Contains("VisibleModels()", StringComparison.Ordinal) && tab.Contains("sp.IsShown", StringComparison.Ordinal));
var zh = File.ReadAllText(FindUp("SpineViewer", "Resources", "Strings", "zh.xaml"));
failed |= Expect("提示不再要求先点选模型", zh.Contains("不用在左侧点选", StringComparison.Ordinal) && !zh.Contains("在左侧用 Ctrl", StringComparison.Ordinal));
}
finally
{
    Directory.Delete(formatRoot, true);
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
