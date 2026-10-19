using System.IO;

namespace PGameLib.Services.Import;

/// <summary>
/// Heuristics for telling a game's launch executable apart from the supporting junk that ships
/// next to it — uninstallers, crash handlers, redistributable installers, setup programs, launch
/// helpers and common development tools. Used by the Start Menu scanner and the folder scan so
/// automatic results stay clean. The user still reviews everything before importing.
/// </summary>
public static class GameExecutableFilter
{
    // Substrings that, when present in a file name, strongly suggest "not the game".
    private static readonly string[] ExcludedNameParts =
    {
        "uninstall", "uninst", "unins000",
        "crashreport", "crashhandler", "crashpad", "crash_report", "bugreport", "errorreport",
        "setup", "installer", "install", "redist", "vcredist", "directx", "dxsetup", "dxwebsetup",
        "dotnet", "vc_redist", "oalinst", "ue4prereqsetup", "ueprereqsetup", "prereq",
        "launcher_installer", "epicgameslauncherinstaller",
        "helper", "service", "updater", "update", "autoupdate", "patcher",
        "diagnostic", "diag", "report", "config", "configtool", "settings", "cleanup",
        "benchmark", "editor", "server_watchdog",
        // development / engine tools
        "devenv", "msbuild", "node", "python", "git", "cmake", "gcc", "clang",
        "unins", "notification_helper", "crashreportclient"
    };

    // Known utility executable names (exact, case-insensitive) that are never the game itself.
    private static readonly HashSet<string> ExcludedExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "unitycrashhandler64.exe", "unitycrashhandler32.exe",
        "crashreportclient.exe", "ueprereqsetup_x64.exe", "ue4prereqsetup_x64.exe",
        "vcredist_x64.exe", "vcredist_x86.exe", "dxsetup.exe", "oalinst.exe",
        "dotnetfx.exe", "vc_redist.x64.exe", "vc_redist.x86.exe",
        "notification_helper.exe", "epicwebhelper.exe",
        "steam.exe", "steamservice.exe", "steamerrorreporter.exe", "steamwebhelper.exe",
        "cef.exe", "cefprocess.exe"
    };

    // Folder name fragments that usually contain only engine/redistributable payloads.
    private static readonly string[] ExcludedFolderParts =
    {
        @"\redist", @"\redistributable", @"\_commonredist", @"\directx", @"\dotnet",
        @"\engine\binaries\thirdparty", @"\__installer", @"\installers"
    };

    /// <summary>Extensions the folder scan treats as a potential launch file.</summary>
    public static readonly string[] LaunchExtensions = { ".exe", ".lnk", ".bat", ".cmd" };

    public static bool IsLikelyGame(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var ext = Path.GetExtension(path);
        if (!LaunchExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            return false;

        var fileName = Path.GetFileName(path);
        if (ExcludedExactNames.Contains(fileName))
            return false;

        var nameNoExt = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        foreach (var part in ExcludedNameParts)
        {
            if (nameNoExt.Contains(part, StringComparison.Ordinal))
                return false;
        }

        var lowerPath = path.ToLowerInvariant();
        foreach (var folderPart in ExcludedFolderParts)
        {
            if (lowerPath.Contains(folderPart, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
