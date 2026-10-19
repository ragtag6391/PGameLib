using System.Windows;
using Microsoft.Win32;
using PGameLib.Services;
using PGameLib.ViewModels;

namespace PGameLib.Views;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/>: file/folder pickers, modal dialogs and
/// message boxes, all parented to the main window so they behave correctly.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner) => _owner = owner;

    public string? PickLaunchFile(string? initialDirectory = null)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select the game's launch file",
            Filter =
                "Game launch files (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|" +
                "Executables (*.exe)|*.exe|" +
                "Shortcuts (*.lnk)|*.lnk|" +
                "Batch / command (*.bat;*.cmd)|*.bat;*.cmd|" +
                "All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
            dlg.InitialDirectory = initialDirectory;

        return dlg.ShowDialog(_owner) == true ? dlg.FileName : null;
    }

    public string? PickImageFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose a cover image",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true
        };
        return dlg.ShowDialog(_owner) == true ? dlg.FileName : null;
    }

    public string? PickFolder(string? title = null)
    {
        // OpenFolderDialog is built into WPF on .NET 8 and later.
        var dlg = new OpenFolderDialog
        {
            Title = title ?? "Select a folder",
            Multiselect = false
        };
        return dlg.ShowDialog(_owner) == true ? dlg.FolderName : null;
    }

    public bool ShowAddEditDialog(AddEditGameViewModel viewModel)
    {
        var window = new AddEditGameWindow { DataContext = viewModel, Owner = _owner };
        return window.ShowDialog() == true;
    }

    public bool ShowScanResultsDialog(ScanResultsViewModel viewModel)
    {
        var window = new ScanResultsWindow { DataContext = viewModel, Owner = _owner };
        return window.ShowDialog() == true;
    }

    public void ShowError(string message, string title = "PGameLib")
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message, string title = "PGameLib")
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message, string title = "PGameLib")
        => MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
