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

var rendererForDefaults = File.ReadAllText(rendererPath);
var shader = File.ReadAllText(FindUp("Spine", "Utils", "SFMLShader.cs"));
var loadOptions = File.ReadAllText(FindUp("SpineViewer", "Models", "SpineObjectModel.cs"));
var preferences = File.ReadAllText(FindUp("SpineViewer", "Models", "PreferenceModel.cs"));
var textureLoader = File.ReadAllText(FindUp("Spine", "Implementations", "TextureLoader.cs"));
failed |= Expect("动画页保留单模型轨道下拉", xaml.Contains("SelectedValue=\"{Binding AnimationName}\"", StringComparison.Ordinal));
failed |= Expect("动画页不再有联动控件", !xaml.Contains("LinkedAnimation", StringComparison.Ordinal));
var tab = File.ReadAllText(FindUp("SpineViewer", "ViewModels", "Main", "SpineObjectTabViewModel.cs"));
failed |= Expect("属性页不再订阅联动刷新", !tab.Contains("RefreshLinkedAnimations", StringComparison.Ordinal) && !tab.Contains("LinkedAnimation", StringComparison.Ordinal));
failed |= Expect("播放速度默认 0.77 并乘进帧时间", rendererForDefaults.Contains("_speed = 0.77f", StringComparison.Ordinal) && rendererForDefaults.Contains("delta *= _speed", StringComparison.Ordinal));
failed |= Expect("新模型默认打开预乘 Alpha 开关", loadOptions.Contains("public bool UsePma { get; set; } = true;", StringComparison.Ordinal));
failed |= Expect("首选项里的预乘 Alpha 默认打开", preferences.Contains("private bool _usePma = true;", StringComparison.Ordinal));
failed |= Expect("纹理加载的强制预乘仍默认关闭", textureLoader.Contains("public bool ForcePremul { get; set; }", StringComparison.Ordinal) && !textureLoader.Contains("ForcePremul { get; set; } = true", StringComparison.Ordinal));
var pmaShader = shader.Substring(shader.IndexOf("FRAGMENT_VertexAlphaPma", StringComparison.Ordinal), 220);
failed |= Expect("预乘着色器不再用纹理 Alpha 乘一次颜色", pmaShader.Contains("p.rgb *= gl_Color.a;", StringComparison.Ordinal) && !pmaShader.Contains("p.a *", StringComparison.Ordinal));
failed |= Expect("关闭时渲染等待有超时", rendererForDefaults.Contains("Task.WaitAll", StringComparison.Ordinal) && rendererForDefaults.Contains("TimeSpan.FromSeconds(3)", StringComparison.Ordinal));
failed |= Expect("视区复制仍在", rendererForDefaults.Contains("new SFML.Graphics.View(liveView)", StringComparison.Ordinal));
failed |= Expect("播放进度条仍可拖动并双向绑定", xaml.Contains("x:Name=\"_playbackSlider\"", StringComparison.Ordinal) && xaml.Contains("PlaybackPosition, Mode=TwoWay", StringComparison.Ordinal) && xaml.Contains("IsMoveToPointEnabled=\"True\"", StringComparison.Ordinal) && xaml.Contains("IsEnabled=\"{Binding CanScrub}\"", StringComparison.Ordinal));
failed |= Expect("播放进度条比原来更矮", xaml.Contains("<Slider x:Name=\"_playbackSlider\"", StringComparison.Ordinal) && xaml.Contains("Height=\"16\"", StringComparison.Ordinal));
var windowCode = File.ReadAllText(FindUp("SpineViewer", "Views", "MainWindow.xaml.cs"));
failed |= Expect("拖动进度条仍会暂停回写并在松开后结束", windowCode.Contains("BeginPlaybackScrub()", StringComparison.Ordinal) && windowCode.Contains("EndPlaybackScrub()", StringComparison.Ordinal));
failed |= Expect("保存用户状态同时留下速度和最近打开", windowCode.Contains("Speed = _vm.SFMLRendererViewModel.Speed", StringComparison.Ordinal) && windowCode.Contains("RecentOpenedFiles = _vm.FileBrowserViewModel.RecentPaths.ToList()", StringComparison.Ordinal));
var loadAt = windowCode.IndexOf("private void LoadUserState", StringComparison.Ordinal);
var saveAt = windowCode.IndexOf("private void SaveUserState", StringComparison.Ordinal);
var loadBody = loadAt >= 0 && saveAt > loadAt ? windowCode.Substring(loadAt, saveAt - loadAt) : "";
failed |= Expect("启动时只恢复最近打开记录而不自动加载", loadBody.Contains("ReplaceRecent(m.RecentOpenedFiles)", StringComparison.Ordinal) && !loadBody.Contains("LoadRecent", StringComparison.Ordinal));

var historyRoot = Path.Combine(Path.GetTempPath(), "sv-recent-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(historyRoot);
var kept = Path.Combine(historyRoot, "kept.skel");
File.WriteAllText(kept, "skel");
var first = Spine.Utils.RecentSkeletonHistory.Remember([], [Path.Combine(historyRoot, "a.json"), Path.Combine(historyRoot, "a.atlas"), Path.Combine(historyRoot, "a.png"), kept]);
failed |= Expect("成功的骨骼会记入历史，atlas 和 png 不会", first.Count == 2 && first.Any(p => p.EndsWith("a.json", StringComparison.OrdinalIgnoreCase)) && first.Any(p => p.EndsWith("kept.skel", StringComparison.OrdinalIgnoreCase)) && !first.Any(p => p.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)));
var again = Spine.Utils.RecentSkeletonHistory.Remember(first, [kept.ToUpperInvariant()]);
failed |= Expect("同一路径只保留一条并排到最前", again.Count == 2 && again[0].Equals(Path.GetFullPath(kept), StringComparison.OrdinalIgnoreCase));
IReadOnlyList<string> capped = [];
for (var i = 0; i < 35; i++)
    capped = Spine.Utils.RecentSkeletonHistory.Remember(capped, [Path.Combine(historyRoot, "n" + i + ".skel.bytes")]);
failed |= Expect("历史有上限且最新的在前", capped.Count == Spine.Utils.RecentSkeletonHistory.DefaultCapacity && capped[0].EndsWith("n34.skel.bytes", StringComparison.OrdinalIgnoreCase));
var cleared = Spine.Utils.RecentSkeletonHistory.Remember([], []);
failed |= Expect("清空记录不会删除文件", cleared.Count == 0 && File.Exists(kept));
failed |= Expect("丢失的路径只判定为缺失", Spine.Utils.RecentSkeletonHistory.IsMissing(Path.Combine(historyRoot, "gone.skel")) && !Spine.Utils.RecentSkeletonHistory.IsMissing(kept));
var dupA = Path.Combine(historyRoot, "甲", "hero.skel");
var dupB = Path.Combine(historyRoot, "乙", "hero.skel");
var labels = Spine.Utils.RecentSkeletonHistory.Label(dupA, [dupA, dupB]);
var other = Spine.Utils.RecentSkeletonHistory.Label(dupB, [dupA, dupB]);
failed |= Expect("同名文件能看出不同文件夹", labels.Contains("甲") && other.Contains("乙") && labels != other);
var oldState = """{ "Speed": 0.77, "FileBrowserDirectory": "D:\\\\models", "UseCheckerboardBackground": true }""";
failed |= Expect("旧配置没有最近打开时得到空列表", Spine.Utils.RecentSkeletonHistory.ReadFromUserStateJson(oldState).Count == 0 && oldState.Contains("Speed", StringComparison.Ordinal));
var withRecent = """{ "Speed": 1, "RecentOpenedFiles": [ "D:\\\\models\\\\hero.skel", "D:\\\\models\\\\b.json" ] }""";
var readRecent = Spine.Utils.RecentSkeletonHistory.ReadFromUserStateJson(withRecent);
failed |= Expect("最近打开能从配置里读回且不丢掉其它字段", readRecent.Count == 2 && readRecent[0].EndsWith("hero.skel", StringComparison.Ordinal) && withRecent.Contains("\"Speed\": 1", StringComparison.Ordinal));
Directory.Delete(historyRoot, true);
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
