using PGameLib.Models;

namespace PGameLib.Services;

/// <summary>Persists the game library. Implemented by <see cref="JsonLibraryStore"/>.</summary>
public interface ILibraryStore
{
    Task<LibraryData> LoadAsync();

    Task SaveAsync(LibraryData data);
}
