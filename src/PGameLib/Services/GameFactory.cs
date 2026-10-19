using System.Globalization;
using System.IO;
using PGameLib.Models;

namespace PGameLib.Services;

/// <summary>
/// Builds <see cref="GameEntry"/> objects from a selected launch file. Shared by the
/// "Add Game" dialog and the automatic importers so name/folder detection behaves identically.
/// </summary>
public sealed class GameFactory
{
    private readonly ShortcutResolver _shortcuts;

    public GameFactory(ShortcutResolver shortcuts) => _shortcuts = shortcuts;

    /// <summary>
    /// Creates an entry from any launch file (.exe, .lnk, .bat, .cmd, launcher).
    /// For a shortcut, the install folder is derived from the resolved target.
    /// </summary>
    public GameEntry FromLaunchFile(string launchFile, GameSource source = GameSource.Standalone)
    {
        var entry = new GameEntry
        {
            LaunchPath = launchFile,
            Source = source,
            Name = SuggestName(launchFile)
        };

        // Figure out the install folder.
        string? installFolder = null;

        if (_shortcuts.IsShortcut(launchFile))
        {
            var target = _shortcuts.Resolve(launchFile);
            if (target is not null && !string.IsNullOrWhiteSpace(target.TargetPath))
            {
                installFolder = !string.IsNullOrWhiteSpace(target.WorkingDirectory) && Directory.Exists(target.WorkingDirectory)
                    ? target.WorkingDirectory
                    : Path.GetDirectoryName(target.TargetPath);

                if (!string.IsNullOrWhiteSpace(target.Arguments))
                    entry.LaunchArguments = target.Arguments;
            }
        }

        installFolder ??= Path.GetDirectoryName(launchFile);
        entry.InstallFolder = installFolder ?? string.Empty;

        return entry;
    }

    /// <summary>
    /// Produces a readable name from a file name: strips the extension, replaces separators with
    /// spaces, and title-cases it. "doki_doki-literature-club.exe" -> "Doki Doki Literature Club".
    /// </summary>
    public static string SuggestName(string path)
    {
        var raw = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(raw))
            return "New Game";

        var spaced = raw
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('.', ' ');

        // Insert spaces at camelCase boundaries (e.g. "StardewValley" -> "Stardew Valley").
        var sb = new System.Text.StringBuilder(spaced.Length + 8);
        for (int i = 0; i < spaced.Length; i++)
        {
            var c = spaced[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(spaced[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }

        var collapsed = string.Join(' ', sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(collapsed.ToLowerInvariant());
    }
}
