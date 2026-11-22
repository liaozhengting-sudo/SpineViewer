using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

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

        var entries = new ObservableCollection<string> { "模型", "hero.skel" };
        var list = new ListBox
        {
            ItemsSource = entries,
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

        var generated = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem)
                generated++;
        }

        window.Close();
        app.Shutdown();

        if (list.Items.Count != 2 || generated != 2)
        {
            Console.Error.WriteLine($"items={list.Items.Count} generated={generated}");
            return 1;
        }

        Console.WriteLine("ok wpf list populated 2/2");
        return 0;
    }
}
