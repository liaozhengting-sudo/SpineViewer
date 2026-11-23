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

        if (!ExerciseLastMarkAndRemove(region, text))
        {
            app.Shutdown();
            return 1;
        }

        app.Shutdown();
        Console.WriteLine("ok wpf list populated 4/4 with 3 selected");
        return 0;
    }

    private static bool ExerciseLastMarkAndRemove(SolidColorBrush region, SolidColorBrush text)
    {
        var blue = (Color)ColorConverter.ConvertFromString("#8ECAFF");
        var rows = new ObservableCollection<MarkRow>
        {
            new("40390_skin_Fracture03", true),
            new("其它文件夹", false),
        };
        var files = new ListBox
        {
            ItemsSource = rows,
            SelectionMode = SelectionMode.Extended,
            Width = 220,
            Height = 80,
            Background = region,
            Foreground = text,
            ItemContainerStyle = MarkStyle(),
        };
        var models = new ObservableCollection<string> { "40390_skin_Fracture03", "另一个" };
        var modelList = new ListBox
        {
            ItemsSource = models,
            SelectionMode = SelectionMode.Extended,
            Width = 220,
            Height = 60,
        };
        var settings = new Button { Content = "动画", Width = 80 };
        var remove = new Button { Content = "移除所选模型", Width = 120 };
        var asset = Path.Combine(Path.GetTempPath(), "sv-keep-" + Guid.NewGuid().ToString("N") + ".skel");
        File.WriteAllText(asset, "asset");
        remove.Click += (_, _) =>
        {
            foreach (var name in modelList.SelectedItems.Cast<string>().ToArray())
                models.Remove(name);
        };
        var panel = new StackPanel();
        panel.Children.Add(files);
        panel.Children.Add(modelList);
        panel.Children.Add(settings);
        panel.Children.Add(remove);
        var host = new Window
        {
            Content = panel,
            Width = 260,
            Height = 280,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = true,
        };
        host.Show();
        files.Focus();
        files.SelectedIndex = 0;
        files.UpdateLayout();
        host.UpdateLayout();

        var mark = new List<string> { rows[0].Name };
        static bool ShowBlue(bool chosen, bool selected, bool focused) => chosen && (!selected || !focused);
        if (files.IsKeyboardFocusWithin && ShowBlue(true, true, true))
        {
            Console.Error.WriteLine("focused selection should keep its own readable color");
            host.Close();
            File.Delete(asset);
            return false;
        }

        settings.Focus();
        modelList.SelectedIndex = 0;
        files.UpdateLayout();
        host.UpdateLayout();
        if (mark.Count != 1 || mark[0] != rows[0].Name)
        {
            Console.Error.WriteLine("other controls replaced the file mark");
            host.Close();
            File.Delete(asset);
            return false;
        }
        if (files.IsKeyboardFocusWithin || !ShowBlue(true, files.SelectedIndex == 0, files.IsKeyboardFocusWithin))
        {
            Console.Error.WriteLine($"mark hidden after focus left files focus={files.IsKeyboardFocusWithin}");
            host.Close();
            File.Delete(asset);
            return false;
        }

        var item = (ListBoxItem)files.ItemContainerGenerator.ContainerFromIndex(0);
        var block = FindText(item);
        if (item is null || block is null || item.Foreground is not SolidColorBrush brush || brush.Color != blue)
        {
            var got = item?.Foreground?.ToString() ?? "null";
            Console.Error.WriteLine($"unfocused mark color {got}");
            host.Close();
            File.Delete(asset);
            return false;
        }

        remove.IsEnabled = modelList.SelectedItems.Count > 0;
        if (!remove.IsEnabled)
        {
            Console.Error.WriteLine("remove button stayed disabled with a selection");
            host.Close();
            File.Delete(asset);
            return false;
        }
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        modelList.SelectedIndex = -1;
        remove.IsEnabled = modelList.SelectedItems.Count > 0;
        host.Close();
        var kept = File.Exists(asset);
        File.Delete(asset);
        if (models.Count != 1 || models[0] != "另一个" || remove.IsEnabled || !kept)
        {
            Console.Error.WriteLine($"remove models={models.Count} enabled={remove.IsEnabled} file={kept}");
            return false;
        }
        Console.WriteLine("ok wpf last mark stays after other focus and remove leaves the file");
        return true;
    }

    private static Style MarkStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var unfocused = new MultiDataTrigger();
        unfocused.Conditions.Add(new Condition(new Binding("IsLastChosen"), true));
        unfocused.Conditions.Add(new Condition(new Binding("IsKeyboardFocusWithin") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) }, false));
        unfocused.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8ECAFF"))));
        style.Triggers.Add(unfocused);
        var notSelected = new MultiDataTrigger();
        notSelected.Conditions.Add(new Condition(new Binding("IsLastChosen"), true));
        notSelected.Conditions.Add(new Condition(new Binding("IsSelected") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) }, false));
        notSelected.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8ECAFF"))));
        style.Triggers.Add(notSelected);
        return style;
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
