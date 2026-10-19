using System.IO;
using PGameLib.Models;

namespace PGameLib.Services.Import;

/// <summary>
/// Optional "scan a folder or drive for games" feature. Walks the chosen folder, collects
/// executables that pass <see cref="GameExecutableFilter"/>, and reports them for review.
///
/// To avoid thousands of false positives it keeps at most a couple of candidates per leaf
/// install folder (the largest executables, which are almost always the game binary) and caps
/// the recursion depth so a full-drive scan stays bounded.
/// </summary>
public sealed class FolderScanService
{
    private readonly GameFactory _factory;

    public FolderScanService(GameFactory factory) => _factory = factory;

    public Task<IReadOnlyList<GameEntry>> ScanAsync(
        string root,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run<IReadOnlyList<GameEntry>>(() => Scan(root, progress, ct), ct);

    private IReadOnlyList<GameEntry> Scan(string root, IProgress<string>? progress, CancellationToken ct)
    {
        var results = new List<GameEntry>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return results;

        // Group candidate exes by their immediate folder so we can keep only the best per folder.
        var byFolder = new Dictionary<string, List<FileInfo>>(StringComparer.OrdinalIgnoreCase);

        var stack = new Stack<(string path, int depth)>();
        stack.Push((root, 0));
        const int maxDepth = 8;

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (current, depth) = stack.Pop();

            try
            {
                var attr = File.GetAttributes(current);
                if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    continue;
            }
            catch { continue; }

            progress?.Report(current);

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!GameExecutableFilter.IsLikelyGame(file))
                        continue;

                    try
                    {
                        var fi = new FileInfo(file);
                        // Ignore tiny stubs (< 200 KB) — real game binaries are larger; this trims helpers.
                        if (Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase) && fi.Length < 200 * 1024)
                            continue;

                        var folder = Path.GetDirectoryName(file) ?? current;
                        if (!byFolder.TryGetValue(folder, out var list))
                            byFolder[folder] = list = new List<FileInfo>();
                        list.Add(fi);
                    }
                    catch { /* skip unreadable file */ }
                }
            }
            catch { /* skip unreadable directory */ }

            if (depth < maxDepth)
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(current))
                        stack.Push((dir, depth + 1));
                }
                catch { /* skip unreadable directory */ }
            }
        }

        foreach (var (folder, files) in byFolder)
        {
            ct.ThrowIfCancellationRequested();

            // Keep the two largest executables in each folder as candidates.
            foreach (var fi in files.OrderByDescending(f => f.Length).Take(2))
            {
                var entry = _factory.FromLaunchFile(fi.FullName, GameSource.Standalone);
                entry.InstallFolder = folder;
                results.Add(entry);
            }
        }

        return results
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
