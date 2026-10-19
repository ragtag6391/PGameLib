namespace PGameLib.Helpers;

/// <summary>Formats byte counts as friendly human-readable strings.</summary>
public static class SizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes <= 0) return "—";

        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        // Whole numbers for bytes/KB, one decimal from MB up.
        return unit <= 1
            ? $"{size:0} {Units[unit]}"
            : $"{size:0.0} {Units[unit]}";
    }
}
