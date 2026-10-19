using System.IO;

namespace PGameLib.Helpers;

/// <summary>
/// Produces a canonical key for a file path so that two entries pointing at the same
/// executable are recognised as duplicates even when written differently
/// (trailing slashes, case, forward/back slashes, relative segments, surrounding quotes).
/// </summary>
public static class PathNormalizer
{
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var p = path.Trim().Trim('"');

        // Collapse to an absolute, canonical form where possible. GetFullPath resolves
        // ".", ".." and mixed separators. It does not require the file to exist.
        try
        {
            p = Path.GetFullPath(p);
        }
        catch
        {
            // Leave as-is if the path is malformed; we still normalise case/separators below.
        }

        p = p.Replace('/', '\\').TrimEnd('\\');

        // Windows file system is case-insensitive, so compare case-insensitively.
        return p.ToLowerInvariant();
    }

    /// <summary>True when both paths resolve to the same normalized key.</summary>
    public static bool AreSame(string? a, string? b)
        => Normalize(a).Equals(Normalize(b), StringComparison.Ordinal);
}
