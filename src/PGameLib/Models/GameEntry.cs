using System.Text.Json.Serialization;

namespace PGameLib.Models;

/// <summary>
/// A single library entry. This describes a game; it never owns or contains the game's files.
/// Serialized to JSON under %AppData%\PGameLib\library.json.
/// </summary>
public sealed class GameEntry
{
    /// <summary>Stable identity for the entry, generated once on creation.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display name (editable by the user).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The file that gets launched: an .exe, .lnk shortcut, .bat, .cmd, or other launcher.
    /// This is the raw path exactly as the user selected it (may contain spaces / Unicode).
    /// </summary>
    public string LaunchPath { get; set; } = string.Empty;

    /// <summary>
    /// The installation folder. Detected automatically from the launch file, but editable.
    /// For a shortcut, this is derived from the shortcut's resolved target.
    /// </summary>
    public string InstallFolder { get; set; } = string.Empty;

    /// <summary>Optional launch arguments passed to the process.</summary>
    public string? LaunchArguments { get; set; }

    /// <summary>
    /// Optional working directory override. When empty, the install folder is used.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    public GameSource Source { get; set; } = GameSource.Standalone;

    /// <summary>
    /// A stable, provider-specific id when known (e.g. Steam AppID, Epic AppName).
    /// Helps de-duplicate across refreshes and re-imports.
    /// </summary>
    public string? SourceAppId { get; set; }

    /// <summary>Last computed folder size in bytes. Cached so the UI need not recompute every launch.</summary>
    public long SizeBytes { get; set; }

    /// <summary>When the size was last computed (UTC). Null means "never computed".</summary>
    public DateTime? SizeComputedUtc { get; set; }

    /// <summary>
    /// Path to a cached PNG of the extracted icon, under the app's icon-cache folder.
    /// Null when no icon could be extracted.
    /// </summary>
    public string? IconCachePath { get; set; }

    /// <summary>Optional cover-art image path chosen by the user (overrides the icon on cards).</summary>
    public string? CoverImagePath { get; set; }

    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastPlayedUtc { get; set; }

    /// <summary>
    /// True when the launch file was present at the last refresh. Not serialized as the source
    /// of truth — it's recomputed on load and on every refresh — but kept here so views can bind.
    /// </summary>
    [JsonIgnore]
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// A normalized form of the launch target used only for duplicate detection.
    /// File targets are normalized as paths; URI / shell targets (Steam, Epic, Store) are
    /// compared as lowercased strings since they aren't file-system paths.
    /// Not persisted — always derived from <see cref="LaunchPath"/>.
    /// </summary>
    [JsonIgnore]
    public string NormalizedKey => Helpers.LaunchInspector.IsUri(LaunchPath)
        ? (LaunchPath ?? string.Empty).Trim().ToLowerInvariant()
        : Helpers.PathNormalizer.Normalize(LaunchPath);

    public GameEntry Clone() => (GameEntry)MemberwiseClone();
}
