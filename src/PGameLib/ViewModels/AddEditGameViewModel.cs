using System.IO;
using PGameLib.Helpers;
using PGameLib.Models;
using PGameLib.Services;

namespace PGameLib.ViewModels;

/// <summary>
/// Backs the Add / Edit dialog. Works on a copy of the entry so cancelling discards changes.
/// Also drives the "Add Game" flow: pick a file, suggest a name, detect the folder.
/// </summary>
public sealed class AddEditGameViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly GameFactory _factory;
    private readonly GameEntry _working;
    private readonly bool _isNew;

    public AddEditGameViewModel(IDialogService dialogs, GameFactory factory, GameEntry? existing = null)
    {
        _dialogs = dialogs;
        _factory = factory;
        _isNew = existing is null;
        _working = existing?.Clone() ?? new GameEntry();

        _name = _working.Name;
        _launchPath = _working.LaunchPath;
        _installFolder = _working.InstallFolder;
        _launchArguments = _working.LaunchArguments ?? string.Empty;
        _workingDirectory = _working.WorkingDirectory ?? string.Empty;
        _coverImagePath = _working.CoverImagePath ?? string.Empty;
        _selectedSource = _working.Source;

        BrowseLaunchFileCommand = new RelayCommand(_ => BrowseLaunchFile());
        BrowseInstallFolderCommand = new RelayCommand(_ => BrowseInstallFolder());
        BrowseCoverImageCommand = new RelayCommand(_ => BrowseCoverImage());
        ClearCoverImageCommand = new RelayCommand(_ => CoverImagePath = string.Empty);
        SaveCommand = new RelayCommand(_ => Save(), _ => IsValid);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, false));
    }

    public string Title => _isNew ? "Add Game" : "Edit Game";

    public IReadOnlyList<GameSource> Sources { get; } =
        new[] { GameSource.Standalone, GameSource.Steam, GameSource.Epic, GameSource.Custom };

    private string _name;
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) RaiseValidity(); }
    }

    private string _launchPath;
    public string LaunchPath
    {
        get => _launchPath;
        set { if (SetProperty(ref _launchPath, value)) RaiseValidity(); }
    }

    private string _installFolder;
    public string InstallFolder
    {
        get => _installFolder;
        set => SetProperty(ref _installFolder, value);
    }

    private string _launchArguments;
    public string LaunchArguments
    {
        get => _launchArguments;
        set => SetProperty(ref _launchArguments, value);
    }

    private string _workingDirectory;
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    private string _coverImagePath;
    public string CoverImagePath
    {
        get => _coverImagePath;
        set => SetProperty(ref _coverImagePath, value);
    }

    private GameSource _selectedSource;
    public GameSource SelectedSource
    {
        get => _selectedSource;
        set => SetProperty(ref _selectedSource, value);
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(LaunchPath);

    public RelayCommand BrowseLaunchFileCommand { get; }
    public RelayCommand BrowseInstallFolderCommand { get; }
    public RelayCommand BrowseCoverImageCommand { get; }
    public RelayCommand ClearCoverImageCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>Raised to ask the window to close. Bool is the dialog result (true = saved).</summary>
    public event EventHandler<bool>? RequestClose;

    private void BrowseLaunchFile()
    {
        var initialDir = Directory.Exists(InstallFolder) ? InstallFolder : null;
        var picked = _dialogs.PickLaunchFile(initialDir);
        if (picked is null)
            return;

        LaunchPath = picked;

        // Auto-fill name (only if empty) and install folder using the same logic as the importers.
        var built = _factory.FromLaunchFile(picked, SelectedSource);
        if (string.IsNullOrWhiteSpace(Name))
            Name = built.Name;
        if (string.IsNullOrWhiteSpace(InstallFolder))
            InstallFolder = built.InstallFolder;
        if (string.IsNullOrWhiteSpace(LaunchArguments) && !string.IsNullOrWhiteSpace(built.LaunchArguments))
            LaunchArguments = built.LaunchArguments!;
    }

    private void BrowseInstallFolder()
    {
        var picked = _dialogs.PickFolder("Select the game's installation folder");
        if (picked is not null)
            InstallFolder = picked;
    }

    private void BrowseCoverImage()
    {
        var picked = _dialogs.PickImageFile();
        if (picked is not null)
            CoverImagePath = picked;
    }

    private void Save()
    {
        if (!IsValid)
        {
            _dialogs.ShowError("Please enter a name and choose a launch file.");
            return;
        }

        RequestClose?.Invoke(this, true);
    }

    private void RaiseValidity()
    {
        OnPropertyChanged(nameof(IsValid));
        SaveCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Writes the edited fields back onto the supplied entry (the real library object).
    /// Called by the owner after a successful save.
    /// </summary>
    public void ApplyTo(GameEntry target)
    {
        target.Name = Name.Trim();
        target.LaunchPath = LaunchPath.Trim().Trim('"');
        target.InstallFolder = InstallFolder?.Trim() ?? string.Empty;
        target.LaunchArguments = string.IsNullOrWhiteSpace(LaunchArguments) ? null : LaunchArguments.Trim();
        target.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim();
        target.CoverImagePath = string.IsNullOrWhiteSpace(CoverImagePath) ? null : CoverImagePath.Trim();
        target.Source = SelectedSource;
    }

    /// <summary>Builds a brand-new entry from the dialog's fields (for the Add flow).</summary>
    public GameEntry BuildNewEntry()
    {
        var entry = new GameEntry();
        ApplyTo(entry);
        return entry;
    }
}
