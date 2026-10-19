using PGameLib.ViewModels;

namespace PGameLib.Services;

/// <summary>
/// Abstracts the few places the view models need to talk to the window layer (file pickers,
/// modal dialogs, message boxes). Keeping it behind an interface means the view models carry no
/// direct reference to WPF windows and could be unit-tested with a fake implementation.
/// </summary>
public interface IDialogService
{
    /// <summary>Opens a file picker for a launch file (EXE, shortcut, BAT, CMD, launcher). Returns null if cancelled.</summary>
    string? PickLaunchFile(string? initialDirectory = null);

    /// <summary>Opens a file picker restricted to images, for choosing cover art. Returns null if cancelled.</summary>
    string? PickImageFile();

    /// <summary>Opens a folder picker. Returns null if cancelled.</summary>
    string? PickFolder(string? title = null);

    /// <summary>Shows the Add/Edit dialog modally. Returns true if the user saved.</summary>
    bool ShowAddEditDialog(AddEditGameViewModel viewModel);

    /// <summary>Shows the scan-results review dialog modally. Returns true if the user imported.</summary>
    bool ShowScanResultsDialog(ScanResultsViewModel viewModel);

    void ShowError(string message, string title = "PGameLib");

    void ShowInfo(string message, string title = "PGameLib");

    bool Confirm(string message, string title = "PGameLib");
}
