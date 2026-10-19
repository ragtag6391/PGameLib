using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PGameLib.Helpers;
using PGameLib.Models;

namespace PGameLib.Services;

public sealed record LaunchResult(bool Success, string? ErrorMessage)
{
    public static LaunchResult Ok() => new(true, null);
    public static LaunchResult Fail(string message) => new(false, message);
}

/// <summary>
/// Launches a game through ShellExecute (UseShellExecute = true), which transparently handles
/// .exe, .lnk shortcuts, .bat/.cmd scripts and registered launchers. Paths with spaces and
/// Unicode characters work because the raw path is assigned to FileName (ShellExecute does its
/// own quoting); custom arguments and a working directory are honoured when provided.
/// </summary>
public sealed class GameLauncher
{
    public LaunchResult Launch(GameEntry game)
    {
        if (game is null)
            return LaunchResult.Fail("No game was selected.");

        var path = game.LaunchPath?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            return LaunchResult.Fail($"\"{game.Name}\" has no launch target set. Edit the game and choose its executable.");

        // Steam / Epic entries launch through a protocol URI handled by the store client.
        if (LaunchInspector.IsUri(path))
            return LaunchUri(game, path);

        if (!File.Exists(path))
            return LaunchResult.Fail(
                $"The launch file for \"{game.Name}\" was not found:\n{path}\n\n" +
                "The game may have been moved or uninstalled. Use \"Locate\" to point it at the new file.");

        // Pick the working directory: explicit override, else the install folder, else the file's folder.
        var workingDir = FirstExistingDirectory(
            game.WorkingDirectory,
            game.InstallFolder,
            Path.GetDirectoryName(path));

        var psi = new ProcessStartInfo
        {
            FileName = path,                         // raw path — ShellExecute quotes it itself
            UseShellExecute = true,                  // required for .lnk / .bat / registered launchers
            WorkingDirectory = workingDir ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(game.LaunchArguments))
            psi.Arguments = game.LaunchArguments;

        try
        {
            Process.Start(psi);
            game.LastPlayedUtc = DateTime.UtcNow;
            return LaunchResult.Ok();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED — the user dismissed a UAC / elevation prompt. Not an error worth shouting about.
            return LaunchResult.Fail("Launch was cancelled.");
        }
        catch (Win32Exception ex)
        {
            return LaunchResult.Fail($"Windows couldn't start \"{game.Name}\":\n{ex.Message}");
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail($"Couldn't start \"{game.Name}\":\n{ex.Message}");
        }
    }

    private LaunchResult LaunchUri(GameEntry game, string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            game.LastPlayedUtc = DateTime.UtcNow;
            return LaunchResult.Ok();
        }
        catch (Win32Exception ex)
        {
            return LaunchResult.Fail(
                $"Couldn't start \"{game.Name}\" through its store client:\n{ex.Message}\n\n" +
                "Make sure the Steam or Epic client is installed.");
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail($"Couldn't start \"{game.Name}\":\n{ex.Message}");
        }
    }

    /// <summary>Opens the game's install folder in Explorer, selecting the launch file when possible.</summary>
    public LaunchResult OpenFolder(GameEntry game)
    {
        var folder = FirstExistingDirectory(game.InstallFolder, Path.GetDirectoryName(game.LaunchPath?.Trim().Trim('"')));
        if (folder is null)
            return LaunchResult.Fail(
                $"The folder for \"{game.Name}\" couldn't be found. It may have been moved or removed.");

        try
        {
            // If the launch file exists, select it; otherwise just open the folder.
            var launch = game.LaunchPath?.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(launch) && File.Exists(launch))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{launch}\"") { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            return LaunchResult.Ok();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail($"Couldn't open the folder for \"{game.Name}\":\n{ex.Message}");
        }
    }

    private static string? FirstExistingDirectory(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && Directory.Exists(c))
                return c;
        }
        return null;
    }
}
