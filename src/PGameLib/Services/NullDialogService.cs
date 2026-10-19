using PGameLib.ViewModels;

namespace PGameLib.Services;

/// <summary>
/// A do-nothing dialog service used as a safe placeholder before the real (WPF) dialog service
/// is attached, and in any headless/test context. Every method is a no-op that cancels.
/// </summary>
public sealed class NullDialogService : IDialogService
{
    public static readonly NullDialogService Instance = new();

    public string? PickLaunchFile(string? initialDirectory = null) => null;
    public string? PickImageFile() => null;
    public string? PickFolder(string? title = null) => null;
    public bool ShowAddEditDialog(AddEditGameViewModel viewModel) => false;
    public bool ShowScanResultsDialog(ScanResultsViewModel viewModel) => false;
    public void ShowError(string message, string title = "PGameLib") { }
    public void ShowInfo(string message, string title = "PGameLib") { }
    public bool Confirm(string message, string title = "PGameLib") => false;
}
