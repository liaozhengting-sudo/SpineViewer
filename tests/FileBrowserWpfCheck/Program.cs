using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FileBrowserWpfCheck;

public static class Program
{
    // 在 Windows 上确认列表绑定后能生成可见条目。不启动 SpineViewer 主窗口。
    [STAThread]
    public static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml", UriKind.Absolute),
        });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute),
        });
        var region = (SolidColorBrush)app.FindResource("RegionBrush");
        var text = (SolidColorBrush)app.FindResource("PrimaryTextBrush");
        static double Lum(System.Windows.Media.Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        Console.WriteLine($"RegionBrush #{region.Color.R:X2}{region.Color.G:X2}{region.Color.B:X2} lum={Lum(region.Color):0.00}");
        Console.WriteLine($"PrimaryTextBrush #{text.Color.R:X2}{text.Color.G:X2}{text.Color.B:X2} lum={Lum(text.Color):0.00}");
        if (Lum(region.Color) > 0.35 || Lum(text.Color) < 0.55)
        {
            Console.Error.WriteLine("dark theme contrast failed");
            return 1;
        }
        var visited = (Color)ColorConverter.ConvertFromString("#8ECAFF");
        if (Lum(visited) < 0.55 || Math.Abs(Lum(visited) - Lum(text.Color)) < 0.08)
        {
            Console.Error.WriteLine("visited folder color is not distinct on the dark surface");
            return 1;
        }

        var entries = new ObservableCollection<string> { "模型", "甲", "乙", "hero.skel" };
        var list = new ListBox
        {
            ItemsSource = entries,
            SelectionMode = SelectionMode.Extended,
            Width = 240,
            Height = 180,
            Background = region,
            Foreground = text,
        };
        var window = new Window
        {
            Content = list,
            Width = 280,
            Height = 240,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        list.UpdateLayout();

        list.SelectedItems.Add(entries[0]);
        list.SelectedItems.Add(entries[1]);
        list.SelectedItems.Add(entries[2]);
        var keep = list.SelectedItems.Contains(entries[1]);
        if (list.SelectedItems.Count != 3 || !keep)
        {
            Console.Error.WriteLine($"multi-select count={list.SelectedItems.Count}");
            window.Close();
            app.Shutdown();
            return 1;
        }

        var generated = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem)
                generated++;
        }

        window.Close();

        if (list.Items.Count != 4 || generated != 4)
        {
            Console.Error.WriteLine($"items={list.Items.Count} generated={generated}");
            app.Shutdown();
            return 1;
        }

        var stylePath = FindRepoFile("SpineViewer", "Resources", "FileBrowserListStyles.xaml");
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(stylePath, UriKind.Absolute),
        });
        if (!ExerciseThemedFileList(app, region))
        {
            app.Shutdown();
            return 1;
        }

        app.Shutdown();
        Console.WriteLine("ok wpf list populated 4/4 with 3 selected");
        return 0;
    }

    private static string FindRepoFile(params string[] parts)
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

    private static bool ExerciseThemedFileList(Application app, SolidColorBrush region)
    {
        var blue = (Color)ColorConverter.ConvertFromString("#8ECAFF");
        var rows = new ObservableCollection<MarkRow>
        {
            new("A-70130", true),
            new("B-folder", false),
        };
        var files = new ListBox
        {
            ItemsSource = rows,
            SelectionMode = SelectionMode.Extended,
            Width = 280,
            Height = 90,
            Background = region,
            Foreground = (Brush)app.FindResource("PrimaryTextBrush"),
            ItemContainerStyle = (Style)app.FindResource("FileBrowserItemStyle"),
            ItemTemplate = (DataTemplate)app.FindResource("FileBrowserItemTemplate"),
        };
        var models = new ObservableCollection<string> { "40390_skin_Fracture03", "另一个" };
        var modelList = new ListBox { ItemsSource = models, SelectionMode = SelectionMode.Extended, Height = 50 };
        var animation = new Button { Content = "动画", Width = 80 };
        var panel = new StackPanel();
        panel.Children.Add(files);
        panel.Children.Add(modelList);
        panel.Children.Add(animation);
        var host = new Window
        {
            Content = panel,
            Width = 320,
            Height = 240,
            Background = region,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = true,
        };
        host.Show();
        files.Focus();
        files.SelectedIndex = 0;
        files.UpdateLayout();
        host.UpdateLayout();

        if (!ContrastOk(files, 0, blue, region.Color, "A focused last mark"))
        {
            host.Close();
            return false;
        }
        if (!ContrastOk(files, 1, null, region.Color, "B normal light text"))
        {
            host.Close();
            return false;
        }

        animation.Focus();
        modelList.SelectedIndex = 0;
        files.UpdateLayout();
        host.UpdateLayout();
        if (!ContrastOk(files, 0, blue, region.Color, "A still blue after other panels"))
        {
            host.Close();
            return false;
        }

        rows[0] = new MarkRow("A-70130", false);
        rows[1] = new MarkRow("B-folder", true);
        files.SelectedIndex = 1;
        files.UpdateLayout();
        host.UpdateLayout();
        if (!ContrastOk(files, 1, blue, region.Color, "B becomes the only blue"))
        {
            host.Close();
            return false;
        }
        if (!ContrastOk(files, 0, null, region.Color, "A returns to theme text"))
        {
            host.Close();
            return false;
        }

        SaveShot(host, Path.Combine(AppContext.BaseDirectory, "file-list-contrast.png"));
        host.Close();
        Console.WriteLine("ok themed file list contrast");
        return true;
    }

    private static bool ContrastOk(ListBox list, int index, Color? expect, Color page, string name)
    {
        var item = list.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
        var block = FindText(item);
        if (item is null || block?.Foreground is not SolidColorBrush brush)
        {
            Console.Error.WriteLine($"{name}: missing text");
            return false;
        }
        var bg = item.Background as SolidColorBrush;
        var bgColor = bg?.Color ?? page;
        var lum = Lum(brush.Color);
        var contrast = Contrast(brush.Color, bgColor);
        Console.WriteLine($"{name}: fg=#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2} bg=#{bgColor.R:X2}{bgColor.G:X2}{bgColor.B:X2} contrast={contrast:0.00}");
        if (brush.Color.R < 40 && brush.Color.G < 40 && brush.Color.B < 40)
        {
            Console.Error.WriteLine($"{name}: text is black");
            return false;
        }
        if (contrast < 4.5)
        {
            Console.Error.WriteLine($"{name}: contrast too low");
            return false;
        }
        if (expect is Color want && brush.Color != want)
        {
            Console.Error.WriteLine($"{name}: expected #{want.R:X2}{want.G:X2}{want.B:X2}");
            return false;
        }
        if (expect is null && brush.Color == (Color)ColorConverter.ConvertFromString("#8ECAFF"))
        {
            Console.Error.WriteLine($"{name}: unexpected blue");
            return false;
        }
        if (lum < 0.45)
        {
            Console.Error.WriteLine($"{name}: text too dark");
            return false;
        }
        return true;
    }

    private static double Lum(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    private static double Contrast(Color a, Color b)
    {
        double Linear(double channel)
        {
            var s = channel / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        double L(Color c) => 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        var hi = Math.Max(L(a), L(b));
        var lo = Math.Min(L(a), L(b));
        return (hi + 0.05) / (lo + 0.05);
    }

    private static void SaveShot(Window host, string path)
    {
        var width = Math.Max(1, (int)host.ActualWidth);
        var height = Math.Max(1, (int)host.ActualHeight);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        Console.WriteLine("screenshot " + path);
    }

    private static TextBlock? FindText(DependencyObject? node)
    {
        if (node is null) return null;
        if (node is TextBlock block) return block;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var found = FindText(VisualTreeHelper.GetChild(node, i));
            if (found is not null) return found;
        }
        return null;
    }

    private sealed class MarkRow
    {
        public MarkRow(string name, bool chosen)
        {
            Name = name;
            IsLastChosen = chosen;
        }
        public string Name { get; }
        public bool IsLastChosen { get; }
    }
}
