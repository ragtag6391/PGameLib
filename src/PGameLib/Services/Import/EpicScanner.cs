using System.IO;
using System.Text.Json;
using PGameLib.Models;

namespace PGameLib.Services.Import;

/// <summary>
/// Discovers Epic Games installations by reading the launcher's per-app manifest files
/// (ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item). Each manifest is JSON and
/// names the install location and the launch executable, so Epic games get a real .exe path
/// (which also means proper icon extraction and a straightforward availability check).
/// </summary>
public sealed class EpicScanner
{
    private sealed class EpicManifest
    {
        public string? InstallLocation { get; set; }
        public string? LaunchExecutable { get; set; }
        public string? LaunchCommand { get; set; }
        public string? DisplayName { get; set; }
        public string? AppName { get; set; }
    }

    public IReadOnlyList<GameEntry> Scan()
    {
        var results = new List<GameEntry>();

        var manifestsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(manifestsDir))
            return results;

        string[] files;
        try
        {
            files = Directory.GetFiles(manifestsDir, "*.item");
        }
        catch
        {
            return results;
        }

        foreach (var file in files)
        {
            var entry = Parse(file);
            if (entry is not null)
                results.Add(entry);
        }

        return results;
    }

    private static GameEntry? Parse(string itemFile)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<EpicManifest>(
                File.ReadAllText(itemFile),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest is null ||
                string.IsNullOrWhiteSpace(manifest.InstallLocation) ||
                string.IsNullOrWhiteSpace(manifest.LaunchExecutable))
                return null;

            var exe = Path.Combine(manifest.InstallLocation, manifest.LaunchExecutable);

            return new GameEntry
            {
                Name = string.IsNullOrWhiteSpace(manifest.DisplayName)
                    ? GameFactory.SuggestName(manifest.LaunchExecutable)
                    : manifest.DisplayName!,
                Source = GameSource.Epic,
                SourceAppId = manifest.AppName,
                LaunchPath = exe,
                InstallFolder = manifest.InstallLocation!,
                LaunchArguments = string.IsNullOrWhiteSpace(manifest.LaunchCommand) ? null : manifest.LaunchCommand
            };
        }
        catch
        {
            return null;
        }
    }
}
