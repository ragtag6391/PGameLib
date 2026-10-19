using System.IO;

namespace PGameLib.Helpers;

/// <summary>
/// Central place for the app's on-disk locations. Everything lives under
/// %AppData%\PGameLib so uninstalling leaves no scattered state.
/// </summary>
public static class AppPaths
{
    private static readonly object FolderLock = new();
    private static bool _migrationChecked;

    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PGameLib");

    public static string LibraryFile => Path.Combine(RootFolder, "library.json");

    public static string IconCacheFolder => Path.Combine(RootFolder, "icons");

    public static string SettingsFile => Path.Combine(RootFolder, "settings.json");

    public static void EnsureFolders()
    {
        lock (FolderLock)
        {
            if (!_migrationChecked)
            {
                MigrateLegacyData();
                _migrationChecked = true;
            }

            Directory.CreateDirectory(RootFolder);
            Directory.CreateDirectory(IconCacheFolder);
        }
    }

    private static void MigrateLegacyData()
    {
        if (Directory.Exists(RootFolder))
            return;

        var legacyFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyGameLibrary");

        if (!Directory.Exists(legacyFolder))
            return;

        try
        {
            Directory.Move(legacyFolder, RootFolder);
        }
        catch (IOException)
        {
            // If the old folder is in use, keep it intact and start with the new location.
        }
        catch (UnauthorizedAccessException)
        {
            // A permissions issue should not prevent the app from starting.
        }
    }
}
