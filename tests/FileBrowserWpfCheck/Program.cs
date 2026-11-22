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
        var entries = new ObservableCollection<string> { "模型", "hero.skel" };
        var list = new ListBox
        {
            ItemsSource = entries,
            Width = 240,
            Height = 180,
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
