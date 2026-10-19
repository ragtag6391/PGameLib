using System.IO;
using PGameLib.Helpers;
using PGameLib.Models;

namespace PGameLib.Services.Import;

/// <summary>
/// Builds library entries for a fixed list of well-known games, but ONLY for the ones whose
/// launch target can actually be found on this machine right now. Runs once on first launch.
///
/// Each detector uses a concrete, documented strategy (Steam AppID, Epic manifest, a known
/// install path, or a Microsoft Store package folder). If a game can't be located it is simply
/// skipped — nothing is invented and no placeholder entry is created.
/// </summary>
public sealed class PreloadSeeder
{
    public IReadOnlyList<GameEntry> BuildPreloadEntries(
        IReadOnlyList<GameEntry> steamGames,
        IReadOnlyList<GameEntry> epicGames)
    {
        var found = new List<GameEntry>();

        void AddIfNotNull(GameEntry? e)
        {
            if (e is not null) found.Add(e);
        }

        // --- Steam-based titles: match by AppID, fall back to name contains ---
        AddIfNotNull(FromSteam(steamGames, "2767030", "Marvel Rivals"));
        AddIfNotNull(FromSteam(steamGames, "698780", "Doki Doki Literature Club")
                     ?? FromStandaloneFolderProbe("Doki Doki Literature Club",
                            new[] { "DDLC.exe", "Doki Doki Literature Club.exe" }));
        AddIfNotNull(FromSteam(steamGames, "367520", "Hollow Knight")
                     ?? FromEpic(epicGames, "Hollow Knight"));
        AddIfNotNull(FromSteam(steamGames, "413150", "Stardew Valley")
                     ?? FromStandaloneFolderProbe("Stardew Valley", new[] { "Stardew Valley.exe", "StardewModdingAPI.exe" }));
        AddIfNotNull(FromSteam(steamGames, "391540", "Undertale"));
        AddIfNotNull(FromSteam(steamGames, null, "SCRAPCRAFT")
                     ?? FromStandaloneFolderProbe("SCRAPCRAFT", new[] { "SCRAPCRAFT.exe", "ScrapCraft.exe" }));

        // --- Epic-based titles ---
        AddIfNotNull(FromEpic(epicGames, "Fortnite"));

        // --- Launcher / standalone titles found by known paths ---
        AddIfNotNull(DetectTLauncherMinecraft());
        AddIfNotNull(DetectRoblox());

        // --- Microsoft Store (UWP) title ---
        AddIfNotNull(DetectMicrosoftSolitaire());

        return found;
    }

    private static GameEntry? FromSteam(IReadOnlyList<GameEntry> steam, string? appId, string nameContains)
    {
        var match = steam.FirstOrDefault(g =>
            (appId is not null && string.Equals(g.SourceAppId, appId, StringComparison.OrdinalIgnoreCase)) ||
            g.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

        // Steam entries are available via the install folder; only seed when it actually exists.
        if (match is null) return null;
        if (!string.IsNullOrWhiteSpace(match.InstallFolder) && !Directory.Exists(match.InstallFolder))
            return null;

        return match.Clone();
    }

    private static GameEntry? FromEpic(IReadOnlyList<GameEntry> epic, string nameContains)
    {
        var match = epic.FirstOrDefault(g => g.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
        if (match is null) return null;

        // Epic entries use a real exe path — seed only when the file exists.
        return LaunchInspector.IsAvailable(match) ? match.Clone() : null;
    }

    /// <summary>Looks for a standalone game in common install roots by probing for known exe names.</summary>
    private static GameEntry? FromStandaloneFolderProbe(string displayName, string[] exeNames)
    {
        foreach (var root in CommonGameRoots())
        {
            foreach (var exe in exeNames)
            {
                // Check a direct "<root>\<DisplayName>\<exe>" layout first (cheap).
                var direct = Path.Combine(root, displayName, exe);
                if (File.Exists(direct))
                    return MakeStandalone(displayName, direct);
            }
        }
        return null;
    }

    private static GameEntry? DetectTLauncherMinecraft()
    {
        // TLauncher.exe commonly sits on the Desktop, in Downloads, or under %APPDATA%.
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "TLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TLauncher", "TLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TLauncher", "TLauncher.exe")
        };

        var exe = candidates.FirstOrDefault(File.Exists);
        return exe is null ? null : MakeStandalone("Minecraft (TLauncher)", exe);
    }

    private static GameEntry? DetectRoblox()
    {
        // Roblox installs player builds under %LOCALAPPDATA%\Roblox\Versions\<hash>\RobloxPlayerBeta.exe.
        var versionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "Versions");

        if (!Directory.Exists(versionsDir))
            return null;

        try
        {
            var exe = Directory.EnumerateFiles(versionsDir, "RobloxPlayerBeta.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            return exe is null ? null : MakeStandalone("Roblox", exe);
        }
        catch
        {
            return null;
        }
    }

    private static GameEntry? DetectMicrosoftSolitaire()
    {
        // Microsoft Store apps keep per-user data under %LOCALAPPDATA%\Packages\<PackageFamilyName>.
        // That folder existing is a reliable, non-admin signal the app is installed and has run.
        const string familyName = "Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe";
        var packageFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", familyName);

        if (!Directory.Exists(packageFolder))
            return null;

        return new GameEntry
        {
            Name = "Microsoft Solitaire Collection",
            Source = GameSource.Custom,
            // Launch the Store app through its Application User Model ID via the shell.
            LaunchPath = $@"shell:AppsFolder\{familyName}!App",
            InstallFolder = packageFolder
        };
    }

    private static GameEntry MakeStandalone(string name, string exe) => new()
    {
        Name = name,
        Source = GameSource.Standalone,
        LaunchPath = exe,
        InstallFolder = Path.GetDirectoryName(exe) ?? string.Empty
    };

    private static IEnumerable<string> CommonGameRoots()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrEmpty(pf)) yield return pf;
        if (!string.IsNullOrEmpty(pfx86)) yield return pfx86;
        if (!string.IsNullOrEmpty(local)) yield return Path.Combine(local, "Programs");

        // Common standalone-game drop folders.
        foreach (var drive in new[] { "C", "D", "E" })
        {
            yield return $@"{drive}:\Games";
            yield return $@"{drive}:\";
        }
    }
}
