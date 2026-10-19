using PGameLib.Helpers;
using PGameLib.Models;

namespace PGameLib.ViewModels;

/// <summary>
/// View-model wrapper around a <see cref="GameEntry"/>. Exposes bindable, change-notifying
/// properties for the cards / list rows. The model is the single source of truth; this just
/// surfaces it and raises change notifications when it's updated.
/// </summary>
public sealed class GameCardViewModel : ObservableObject
{
    public GameCardViewModel(GameEntry model)
    {
        Model = model;
    }

    public GameEntry Model { get; }

    public string Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set { if (Model.Name != value) { Model.Name = value; OnPropertyChanged(); } }
    }

    public GameSource Source => Model.Source;

    public string SourceText => Source switch
    {
        GameSource.Steam => "Steam",
        GameSource.Epic => "Epic",
        GameSource.Custom => "Custom",
        _ => "Standalone"
    };

    public string LaunchPath => Model.LaunchPath;

    public string InstallFolder => Model.InstallFolder;

    public string LaunchTargetDisplay => LaunchInspector.IsUri(Model.LaunchPath)
        ? Model.LaunchPath
        : System.IO.Path.GetFileName(Model.LaunchPath);

    public long SizeBytes => Model.SizeBytes;

    public string SizeText => SizeFormatter.Format(Model.SizeBytes);

    /// <summary>The image shown on the card: a chosen cover if present, else the extracted icon.</summary>
    public string? DisplayImagePath =>
        !string.IsNullOrWhiteSpace(Model.CoverImagePath) ? Model.CoverImagePath : Model.IconCachePath;

    public bool IsAvailable
    {
        get => Model.IsAvailable;
        set { if (Model.IsAvailable != value) { Model.IsAvailable = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); } }
    }

    public string StatusText => IsAvailable ? "Ready" : "Missing";

    public DateTime? LastPlayedUtc => Model.LastPlayedUtc;

    public DateTime AddedUtc => Model.AddedUtc;

    /// <summary>Call after the underlying model changes so every derived property re-reads it.</summary>
    public void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(LaunchPath));
        OnPropertyChanged(nameof(InstallFolder));
        OnPropertyChanged(nameof(LaunchTargetDisplay));
        OnPropertyChanged(nameof(SizeBytes));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(DisplayImagePath));
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LastPlayedUtc));
    }

    public void RaiseSizeChanged()
    {
        OnPropertyChanged(nameof(SizeBytes));
        OnPropertyChanged(nameof(SizeText));
    }

    public void RaiseImageChanged() => OnPropertyChanged(nameof(DisplayImagePath));
}
