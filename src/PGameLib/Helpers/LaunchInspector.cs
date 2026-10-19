using System.IO;
using System.Text.RegularExpressions;
using PGameLib.Models;

namespace PGameLib.Helpers;

/// <summary>
/// Understands the two kinds of launch target this app supports:
///  - a real file on disk (.exe, .lnk, .bat, .cmd, launcher), used for standalone/custom/Start-Menu games; and
///  - a protocol URI (steam://rungameid/..., com.epicgames.launcher://...), used for Steam/Epic games,
///    which is how dedicated launchers start store games reliably without guessing the right .exe.
/// </summary>
public static partial class LaunchInspector
{
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*://", RegexOptions.CultureInvariant)]
    private static partial Regex UriSchemeRegex();

    public static bool IsUri(string? launchPath)
    {
        if (string.IsNullOrWhiteSpace(launchPath))
            return false;

        var p = launchPath.Trim();
        // A protocol launch is either a "scheme://..." URI (steam://, com.epicgames.launcher://)
        // or a Windows shell path ("shell:AppsFolder\...!App" for Microsoft Store apps).
        return UriSchemeRegex().IsMatch(p) || p.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the game can currently be launched. For a file target that means the file exists;
    /// for a URI target it means the recorded install folder still exists (or, when no folder is
    /// recorded, we optimistically assume the launcher can handle it).
    /// </summary>
    public static bool IsAvailable(GameEntry game)
    {
        if (game is null) return false;

        if (IsUri(game.LaunchPath))
        {
            return string.IsNullOrWhiteSpace(game.InstallFolder) || Directory.Exists(game.InstallFolder);
        }

        var path = game.LaunchPath?.Trim().Trim('"');
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
