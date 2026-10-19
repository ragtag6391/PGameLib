using PGameLib.Models;

namespace PGameLib.Services.Import;

/// <summary>
/// One entry point for every automatic-import source. The view models call these methods and
/// present the results for review before anything is added. All methods are safe to call off
/// the UI thread.
/// </summary>
public sealed class ImportCoordinator
{
    private readonly ShortcutResolver _shortcuts;
    private readonly GameFactory _factory;
    private readonly SteamScanner _steam;
    private readonly EpicScanner _epic;
    private readonly StartMenuScanner _startMenu;
    private readonly FolderScanService _folderScan;
    private readonly PreloadSeeder _preload;

    public ImportCoordinator()
    {
        _shortcuts = new ShortcutResolver();
        _factory = new GameFactory(_shortcuts);
        _steam = new SteamScanner();
        _epic = new EpicScanner();
        _startMenu = new StartMenuScanner(_shortcuts, _factory);
        _folderScan = new FolderScanService(_factory);
        _preload = new PreloadSeeder();
    }

    public GameFactory Factory => _factory;
    public ShortcutResolver Shortcuts => _shortcuts;

    public Task<IReadOnlyList<GameEntry>> ScanSteamAsync() => Task.Run(() => _steam.Scan());

    public Task<IReadOnlyList<GameEntry>> ScanEpicAsync() => Task.Run(() => _epic.Scan());

    public Task<IReadOnlyList<GameEntry>> ScanStartMenuAsync() => Task.Run(() => _startMenu.Scan());

    public Task<IReadOnlyList<GameEntry>> ScanAllSourcesAsync()
        => Task.Run<IReadOnlyList<GameEntry>>(() =>
        {
            var all = new List<GameEntry>();
            all.AddRange(_steam.Scan());
            all.AddRange(_epic.Scan());
            all.AddRange(_startMenu.Scan());
            return DedupeWithin(all);
        });

    public Task<IReadOnlyList<GameEntry>> ScanFolderAsync(
        string folder, IProgress<string>? progress, CancellationToken ct)
        => _folderScan.ScanAsync(folder, progress, ct);

    /// <summary>Builds the first-run preload entries (only those whose launch target exists).</summary>
    public async Task<IReadOnlyList<GameEntry>> BuildPreloadEntriesAsync()
    {
        var steam = await ScanSteamAsync();
        var epic = await ScanEpicAsync();
        return _preload.BuildPreloadEntries(steam, epic);
    }

    /// <summary>Removes duplicates within a candidate set by normalized launch key.</summary>
    public static IReadOnlyList<GameEntry> DedupeWithin(IEnumerable<GameEntry> candidates)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<GameEntry>();
        foreach (var c in candidates)
        {
            if (seen.Add(c.NormalizedKey))
                result.Add(c);
        }
        return result;
    }
}
