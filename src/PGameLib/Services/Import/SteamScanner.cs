using System.IO;
using PGameLib.Models;

namespace PGameLib.Services.Import;

/// <summary>
/// Discovers installed Steam games by locating the Steam install, reading every library folder
/// from libraryfolders.vdf, and parsing each appmanifest_*.acf. Games are launched through the
/// steam://rungameid/&lt;appid&gt; protocol, which Steam resolves to the correct executable.
/// </summary>
public sealed class SteamScanner
{
    public IReadOnlyList<GameEntry> Scan()
    {
        var results = new List<GameEntry>();

        var steamPath = FindSteamPath();
        if (steamPath is null)
            return results;

        foreach (var library in GetLibraryFolders(steamPath))
        {
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
                continue;

            string[] manifests;
            try
            {
                manifests = Directory.GetFiles(steamApps, "appmanifest_*.acf");
            }
            catch
            {
                continue;
            }

            foreach (var manifest in manifests)
            {
                var entry = ParseManifest(manifest, steamApps);
                if (entry is not null)
                    results.Add(entry);
            }
        }

        return results;
    }

    private static GameEntry? ParseManifest(string manifestPath, string steamAppsFolder)
    {
        try
        {
            var root = VdfParser.Parse(File.ReadAllText(manifestPath));
            var state = root.GetChild("AppState");
            if (state is null)
                return null;

            var appId = state.GetValue("appid");
            var name = state.GetValue("name");
            var installDir = state.GetValue("installdir");

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(installDir))
                return null;

            var installFolder = Path.Combine(steamAppsFolder, "common", installDir);

            return new GameEntry
            {
                Name = string.IsNullOrWhiteSpace(name) ? GameFactory.SuggestName(installDir) : name!,
                Source = GameSource.Steam,
                SourceAppId = appId,
                LaunchPath = $"steam://rungameid/{appId}",
                InstallFolder = installFolder
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? FindSteamPath()
    {
        // Probe the standard Steam install locations. This covers the overwhelming majority of
        // installs without needing any registry access. A Steam folder is one that contains a
        // "steamapps" sub-folder.
        foreach (var candidate in CandidateSteamPaths())
        {
            if (Directory.Exists(Path.Combine(candidate, "steamapps")))
                return candidate;
        }
        return null;
    }

    private static IEnumerable<string> CandidateSteamPaths()
    {
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (!string.IsNullOrEmpty(pfx86)) yield return Path.Combine(pfx86, "Steam");
        if (!string.IsNullOrEmpty(pf)) yield return Path.Combine(pf, "Steam");

        // Common per-drive locations (people often move Steam off C:).
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            var root = drive.RootDirectory.FullName;
            yield return Path.Combine(root, "Steam");
            yield return Path.Combine(root, "SteamLibrary");
            yield return Path.Combine(root, "Program Files (x86)", "Steam");
        }
    }

    private static IEnumerable<string> GetLibraryFolders(string steamPath)
    {
        // The main Steam folder is always a library.
        yield return steamPath;

        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;

        VdfParser.VdfNode root;
        try
        {
            root = VdfParser.Parse(File.ReadAllText(vdf));
        }
        catch
        {
            yield break;
        }

        // Modern format: { "libraryfolders" { "0" { "path" "..." } "1" { "path" "..." } } }
        var container = root.GetChild("libraryfolders") ?? root;
        foreach (var child in container.Children.Values)
        {
            var path = child.GetValue("path");
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                yield return path!;
        }
    }
}
