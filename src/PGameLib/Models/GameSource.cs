namespace PGameLib.Models;

/// <summary>
/// Where a game came from. Used for filtering and for the sidebar sections.
/// </summary>
public enum GameSource
{
    Standalone = 0,
    Steam = 1,
    Epic = 2,
    Custom = 3
}
