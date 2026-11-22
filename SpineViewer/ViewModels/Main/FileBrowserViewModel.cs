using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spine;
using SpineViewer.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace SpineViewer.ViewModels.Main
{
    public class FileBrowserEntry
    {
        public FileBrowserEntry(string name, string fullPath, bool isDirectory)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
        }

        public string Name { get; }
        public string FullPath { get; }
        public bool IsDirectory { get; }
        public bool IsSpineFile => !IsDirectory;
    }

    /// <summary>
    /// 只浏览一个文件夹的当前层，列出子文件夹和骨骼文件。拖放文件夹只是打开它，不批量加载。
    /// </summary>
    public class FileBrowserViewModel : ObservableObject
    {
        private readonly MainWindowViewModel _vmMain;
        private int _listingGeneration;

        public FileBrowserViewModel(MainWindowViewModel vmMain)
        {
            _vmMain = vmMain;
        }

        public ObservableCollection<FileBrowserEntry> Entries { get; } = [];

        public string? CurrentDirectory
        {
            get => _currentDirectory;
            private set => SetProperty(ref _currentDirectory, value);
        }
        private string? _currentDirectory;

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }
        private string _statusText = "";

        public bool IsListing
        {
            get => _isListing;
            private set => SetProperty(ref _isListing, value);
        }
        private bool _isListing;

        public RelayCommand Cmd_OpenFolder => _cmd_OpenFolder ??= new(() =>
        {
            if (!DialogService.ShowOpenFolderDialog(out var folder) || string.IsNullOrWhiteSpace(folder))
                return;
            OpenDirectory(folder);
        });
        private RelayCommand? _cmd_OpenFolder;

        public RelayCommand Cmd_Up => _cmd_Up ??= new(() =>
        {
            if (string.IsNullOrEmpty(CurrentDirectory))
                return;
            var parent = Directory.GetParent(CurrentDirectory);
            if (parent is not null)
                OpenDirectory(parent.FullName);
        });
        private RelayCommand? _cmd_Up;

        public RelayCommand Cmd_Refresh => _cmd_Refresh ??= new(() =>
        {
            if (!string.IsNullOrEmpty(CurrentDirectory))
                OpenDirectory(CurrentDirectory);
        });
        private RelayCommand? _cmd_Refresh;

        /// <summary>
        /// 拖进侧边栏的文件或文件夹只切换浏览位置。
        /// </summary>
        public void BrowseDropped(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                if (Directory.Exists(path))
                {
                    OpenDirectory(path);
                    return;
                }
                if (File.Exists(path))
                {
                    var parent = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(parent))
                        OpenDirectory(parent);
                    return;
                }
            }
        }

        /// <summary>
        /// 只加载选中的骨骼文件。选中的如果全是文件夹，则进入第一个文件夹。
        /// </summary>
        public void LoadEntries(IEnumerable<FileBrowserEntry> entries)
        {
            var list = entries.ToArray();
            var files = list.Where(e => e.IsSpineFile).Select(e => e.FullPath).ToArray();
            if (files.Length > 0)
            {
                _vmMain.SpineObjectListViewModel.AddSpineObjectFromFileList(files);
                return;
            }

            var directory = list.FirstOrDefault(e => e.IsDirectory);
            if (directory is not null)
                OpenDirectory(directory.FullPath);
        }

        public void OpenDirectory(string path)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
                return;
            }

            if (!Directory.Exists(fullPath))
            {
                StatusText = fullPath;
                return;
            }

            var generation = ++_listingGeneration;
            CurrentDirectory = fullPath;
            IsListing = true;
            StatusText = fullPath;

            Task.Run(() =>
            {
                var entries = new List<FileBrowserEntry>();
                string? error = null;
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(fullPath))
                    {
                        entries.Add(new FileBrowserEntry(Path.GetFileName(dir), dir, true));
                    }
                    foreach (var file in Directory.EnumerateFiles(fullPath))
                    {
                        if (SpineObject.IsPossibleSkeletonFile(file))
                            entries.Add(new FileBrowserEntry(Path.GetFileName(file), file, false));
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                entries.Sort((a, b) =>
                {
                    var byKind = b.IsDirectory.CompareTo(a.IsDirectory);
                    return byKind != 0 ? byKind : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null)
                    return;
                dispatcher.InvokeAsync(new Action(() =>
                {
                    if (generation != _listingGeneration)
                        return;
                    Entries.Clear();
                    foreach (var entry in entries)
                        Entries.Add(entry);
                    IsListing = false;
                    var spineCount = entries.Count(e => e.IsSpineFile);
                    var folderCount = entries.Count - spineCount;
                    StatusText = error ?? $"{fullPath}    {folderCount} / {spineCount}";
                }));
            });
        }
    }
}
