using System.IO;

namespace PGameLib.Services;

/// <summary>
/// Computes the total size of a folder off the UI thread. Enumeration is resilient:
/// folders it cannot access (permissions, reparse points) are skipped rather than throwing,
/// so one locked sub-folder never aborts the whole measurement.
/// </summary>
public sealed class FolderSizeService
{
    public Task<long> CalculateAsync(string folder, CancellationToken ct = default)
        => Task.Run(() => Calculate(folder, ct), ct);

    public long Calculate(string folder, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return 0;

        long total = 0;

        // Manual stack walk instead of EnumerateFiles("*", AllDirectories) so a single
        // UnauthorizedAccessException deep in the tree doesn't discard the whole result.
        var stack = new Stack<string>();
        stack.Push(folder);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = stack.Pop();

            // Skip reparse points (junctions / symlinks) to avoid double counting and loops.
            try
            {
                var attr = File.GetAttributes(current);
                if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    continue;
            }
            catch
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Unreadable individual file — ignore and keep going.
                    }
                }
            }
            catch
            {
                // Unreadable directory listing — ignore.
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(current))
                    stack.Push(dir);
            }
            catch
            {
                // Unreadable directory listing — ignore.
            }
        }

        return total;
    }
}
