using System.Diagnostics;
using System.IO;
using System.Reflection;
using PGameLib.Helpers;

namespace PGameLib.ViewModels;

/// <summary>
/// Content for the Settings section: where data lives, a few maintenance actions, and an About
/// blurb. It intentionally only touches the app's own data folder — never any game files.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;

        OpenDataFolderCommand = new RelayCommand(_ => OpenFolder(AppPaths.RootFolder));
        OpenIconCacheCommand = new RelayCommand(_ => OpenFolder(AppPaths.IconCacheFolder));
        ClearIconCacheCommand = new RelayCommand(_ => ClearIconCache());
    }

    public string DataFolder => AppPaths.RootFolder;
    public string LibraryFile => AppPaths.LibraryFile;

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string GamesCountText => _main.GameCountText;
    public string CombinedSizeText => _main.CombinedSizeText;

    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenIconCacheCommand { get; }
    public RelayCommand ClearIconCacheCommand { get; }

    private static void OpenFolder(string folder)
    {
        try
        {
            AppPaths.EnsureFolders();
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // Non-fatal.
        }
    }

    private void ClearIconCache()
    {
        try
        {
            if (Directory.Exists(AppPaths.IconCacheFolder))
            {
                foreach (var file in Directory.EnumerateFiles(AppPaths.IconCacheFolder, "*.png"))
                {
                    try { File.Delete(file); } catch { /* ignore locked file */ }
                }
            }
        }
        catch
        {
            // Non-fatal.
        }
    }
}
