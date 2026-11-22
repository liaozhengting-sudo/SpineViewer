using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spine.Utils;
using SpineViewer.Resources;
using SpineViewer.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace SpineViewer.ViewModels.Main
{
    public class FileBrowserEntry : ObservableObject
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

        public string OpenedMark
        {
            get => _openedMark;
            set => SetProperty(ref _openedMark, value);
        }
        private string _openedMark = "";
    }

    public sealed class RecentOpenedFile
    {
        public RecentOpenedFile(string fullPath, string label, bool missing)
        {
            FullPath = fullPath;
            Label = label;
            Missing = missing;
            Opacity = missing ? 0.55 : 1;
        }

        public string FullPath { get; }
        public string Label { get; }
        public bool Missing { get; }
        public double Opacity { get; }
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
        public ObservableCollection<RecentOpenedFile> RecentFiles { get; } = [];

        public event EventHandler? RecentChanged;

        private readonly object _recentLock = new();
        private List<string> _recentPaths = [];

        public IReadOnlyList<string> RecentPaths
        {
            get { lock (_recentLock) return _recentPaths.ToArray(); }
        }

        public string? CurrentDirectory
        {
            get => _currentDirectory;
            private set => SetProperty(ref _currentDirectory, value);
        }
        private string? _currentDirectory;

        /// <summary>
        /// 地址栏文字。回车或「转到」后才按这个路径重新列出。
        /// </summary>
        public string PathInput
        {
            get => _pathInput;
            set => SetProperty(ref _pathInput, value);
        }
        private string _pathInput = "";

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

        public RelayCommand Cmd_Go => _cmd_Go ??= new(NavigateToInput);
        private RelayCommand? _cmd_Go;

        public void NavigateToInput()
        {
            if (string.IsNullOrWhiteSpace(PathInput))
                return;
            OpenDirectory(PathInput.Trim());
        }

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
        /// 加载选中的骨骼文件或文件夹。文件夹会按加载器支持的格式自动找出骨骼。
        /// </summary>
        public void LoadEntries(IEnumerable<FileBrowserEntry> entries)
        {
            var paths = entries.Select(e => e.FullPath).ToArray();
            if (paths.Length == 0)
                return;
            _vmMain.SpineObjectListViewModel.AddSpineObjectFromFileList(paths);
        }

        public void ReplaceRecent(IEnumerable<string>? paths)
        {
            var next = RecentSkeletonHistory.Remember([], paths ?? []).ToList();
            ApplyRecent(next, save: false);
        }

        public void RememberLoaded(string path)
        {
            if (!RecentSkeletonHistory.ShouldRemember(path))
                return;
            List<string> next;
            lock (_recentLock)
                next = RecentSkeletonHistory.Remember(_recentPaths, [path]).ToList();
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                ApplyRecent(next, save: true);
            else
                dispatcher.BeginInvoke(new Action(() => ApplyRecent(next, save: true)));
        }

        public void ClearRecent()
        {
            ApplyRecent([], save: true);
        }

        public RelayCommand Cmd_ClearRecent => _cmd_ClearRecent ??= new(ClearRecent);
        private RelayCommand? _cmd_ClearRecent;

        public void LoadRecent(RecentOpenedFile file)
        {
            if (RecentSkeletonHistory.IsMissing(file.FullPath))
            {
                StatusText = string.Format(AppResource.Str_RecentOpenedMissing, file.FullPath);
                ApplyRecent(RecentPaths.ToList(), save: false);
                return;
            }
            _vmMain.SpineObjectListViewModel.AddSpineObjectFromFileList([file.FullPath]);
        }

        private void ApplyRecent(List<string> paths, bool save)
        {
            lock (_recentLock)
                _recentPaths = paths;
            RecentFiles.Clear();
            foreach (var path in paths)
            {
                var missing = RecentSkeletonHistory.IsMissing(path);
                RecentFiles.Add(new RecentOpenedFile(path, RecentSkeletonHistory.Label(path, paths), missing));
            }
            UpdateOpenedMarks();
            if (save)
                RecentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateOpenedMarks()
        {
            HashSet<string> opened;
            lock (_recentLock)
                opened = new HashSet<string>(_recentPaths, StringComparer.OrdinalIgnoreCase);
            var mark = AppResource.Str_RecentOpenedMark;
            foreach (var entry in Entries)
                entry.OpenedMark = !entry.IsDirectory && opened.Contains(entry.FullPath) ? mark : "";
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
            PathInput = fullPath;
            IsListing = true;
            StatusText = fullPath;

            Task.Run(() =>
            {
                var listing = SkeletonDirectoryListing.Read(fullPath);
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null)
                    return;
                dispatcher.InvokeAsync(new Action(() => ApplyListing(generation, fullPath, listing)));
            });
        }

        private void ApplyListing(int generation, string fullPath, SkeletonDirectoryListing listing)
        {
            if (generation != _listingGeneration)
                return;
            Entries.Clear();
            foreach (var entry in listing.Entries)
                Entries.Add(new FileBrowserEntry(entry.Name, entry.FullPath, entry.IsDirectory));
            IsListing = false;
            StatusText = Describe(fullPath, listing);
            UpdateOpenedMarks();
        }

        private static string Describe(string fullPath, SkeletonDirectoryListing listing)
        {
            var summary = string.Format(AppResource.Str_FileSidebarStatus, listing.FolderCount, listing.SkeletonCount);
            var detail = listing.Error switch
            {
                SkeletonDirectoryError.NotFound => string.Format(AppResource.Str_FileSidebarNotFound, listing.ErrorDetail ?? fullPath),
                SkeletonDirectoryError.AccessDenied => string.Format(AppResource.Str_FileSidebarAccessDenied, fullPath, listing.ErrorDetail),
                SkeletonDirectoryError.Failed => string.Format(AppResource.Str_FileSidebarReadError, fullPath, listing.ErrorDetail),
                _ when listing.Entries.Count == 0 => AppResource.Str_FileSidebarEmpty,
                _ => summary,
            };
            return listing.Error == SkeletonDirectoryError.None ? detail : detail + " " + summary;
        }
    }
}
