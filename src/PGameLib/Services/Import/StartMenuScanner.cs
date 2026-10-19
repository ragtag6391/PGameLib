using System.IO;
using PGameLib.Models;

namespace PGameLib.Services.Import;

/// <summary>
/// Scans the user and all-users Start Menu for game shortcuts (.lnk). Each shortcut is resolved
/// to its target; obvious non-games (uninstallers, helpers, setup, redistributables) are filtered
/// out. The user reviews the results before anything is imported.
/// </summary>
public sealed class StartMenuScanner
{
    private readonly ShortcutResolver _shortcuts;
    private readonly GameFactory _factory;

    public StartMenuScanner(ShortcutResolver shortcuts, GameFactory factory)
    {
        _shortcuts = shortcuts;
        _factory = factory;
    }

    public IReadOnlyList<GameEntry> Scan()
    {
        var results = new List<GameEntry>();

        foreach (var root in StartMenuRoots())
        {
            if (!Directory.Exists(root))
                continue;

            IEnumerable<string> links;
            try
            {
                links = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var lnk in links)
            {
                var target = _shortcuts.Resolve(lnk);
                if (target is null || string.IsNullOrWhiteSpace(target.TargetPath))
                    continue;

                // Must resolve to an existing executable that looks like a game.
                if (!File.Exists(target.TargetPath))
                    continue;
                if (!GameExecutableFilter.IsLikelyGame(target.TargetPath))
                    continue;

                // Build the entry from the shortcut so the friendly shortcut name is kept.
                var entry = _factory.FromLaunchFile(lnk, GameSource.Standalone);

                // Prefer the shortcut's display name (file name without extension).
                entry.Name = GameFactory.SuggestName(lnk);
                results.Add(entry);
            }
        }

        return results;
    }

    private static IEnumerable<string> StartMenuRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);

        // The "Programs" sub-folder is where most shortcuts actually live.
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
    }
}
