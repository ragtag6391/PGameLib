namespace PGameLib.Models;

public enum ViewMode
{
    Grid = 0,
    List = 1
}

/// <summary>The sidebar sections. "Library" shows everything; the rest filter.</summary>
public enum LibraryFilter
{
    All = 0,
    Steam = 1,
    Epic = 2,
    Standalone = 3,
    Missing = 4,
    Settings = 5
}

public enum SortField
{
    Name = 0,
    SizeBytes = 1,
    LastPlayed = 2,
    AddedDate = 3,
    Source = 4
}
