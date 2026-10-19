namespace PGameLib.Models;

/// <summary>
/// Root object persisted to disk. Wrapping the list lets us add schema/version fields later
/// without breaking older files.
/// </summary>
public sealed class LibraryData
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>True once first-run preload seeding has happened, so it never repeats.</summary>
    public bool PreloadSeeded { get; set; }

    public List<GameEntry> Games { get; set; } = new();
}
