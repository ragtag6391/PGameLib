using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PGameLib.Helpers;
using PGameLib.Models;

namespace PGameLib.Services;

/// <summary>
/// Stores the library as a single JSON file under %AppData%\PGameLib\library.json.
/// Writes are atomic (write to a temp file, then replace) so a crash mid-save can't corrupt it.
/// A SemaphoreSlim serialises concurrent saves triggered by background work.
/// </summary>
public sealed class JsonLibraryStore : ILibraryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public async Task<LibraryData> LoadAsync()
    {
        AppPaths.EnsureFolders();

        if (!File.Exists(AppPaths.LibraryFile))
            return new LibraryData();

        await _ioLock.WaitAsync();
        try
        {
            await using var stream = File.OpenRead(AppPaths.LibraryFile);
            var data = await JsonSerializer.DeserializeAsync<LibraryData>(stream, Options);
            return data ?? new LibraryData();
        }
        catch (Exception)
        {
            // A damaged file should not take the app down. Back it up and start fresh;
            // the user keeps the broken copy in case they want to recover it manually.
            TryBackupCorruptFile();
            return new LibraryData();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveAsync(LibraryData data)
    {
        AppPaths.EnsureFolders();

        await _ioLock.WaitAsync();
        try
        {
            var tempPath = AppPaths.LibraryFile + ".tmp";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, data, Options);
            }

            // Atomic replace where possible; fall back to a plain move on first write.
            if (File.Exists(AppPaths.LibraryFile))
                File.Replace(tempPath, AppPaths.LibraryFile, null);
            else
                File.Move(tempPath, AppPaths.LibraryFile);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private static void TryBackupCorruptFile()
    {
        try
        {
            var backup = AppPaths.LibraryFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Copy(AppPaths.LibraryFile, backup, overwrite: true);
        }
        catch
        {
            // Nothing more we can do; swallow.
        }
    }
}
