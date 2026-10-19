using System.Collections.ObjectModel;
using PGameLib.Helpers;
using PGameLib.Models;

namespace PGameLib.ViewModels;

/// <summary>One reviewable candidate in the scan-results dialog.</summary>
public sealed class ImportCandidate : ObservableObject
{
    public ImportCandidate(GameEntry entry, bool isDuplicate)
    {
        Entry = entry;
        IsDuplicate = isDuplicate;
        _isSelected = !isDuplicate; // duplicates start unchecked
    }

    public GameEntry Entry { get; }

    public bool IsDuplicate { get; }

    public string Name => Entry.Name;
    public string SourceText => Entry.Source.ToString();
    public string LaunchTarget => LaunchInspector.IsUri(Entry.LaunchPath)
        ? Entry.LaunchPath
        : Entry.LaunchPath;
    public string InstallFolder => Entry.InstallFolder;
    public string Note => IsDuplicate ? "Already in library" : string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Shows discovered games for review before importing. Nothing is added until the user confirms;
/// duplicates (already in the library) are flagged and unchecked by default.
/// </summary>
public sealed class ScanResultsViewModel : ObservableObject
{
    public ScanResultsViewModel(string heading, IEnumerable<GameEntry> discovered, ISet<string> existingKeys)
    {
        Heading = heading;

        foreach (var entry in discovered)
        {
            var isDup = existingKeys.Contains(entry.NormalizedKey);
            Candidates.Add(new ImportCandidate(entry, isDup));
        }

        SelectAllCommand = new RelayCommand(_ => SetAll(true));
        SelectNoneCommand = new RelayCommand(_ => SetAll(false));
        ImportCommand = new RelayCommand(_ => RequestClose?.Invoke(this, true), _ => Candidates.Any(c => c.IsSelected));
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, false));

        foreach (var c in Candidates)
            c.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ImportCandidate.IsSelected))
                {
                    ImportCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(SummaryText));
                }
            };
    }

    public string Heading { get; }

    public ObservableCollection<ImportCandidate> Candidates { get; } = new();

    public string SummaryText
    {
        get
        {
            var total = Candidates.Count;
            var selected = Candidates.Count(c => c.IsSelected);
            var dups = Candidates.Count(c => c.IsDuplicate);
            return total == 0
                ? "No games found."
                : $"{total} found · {selected} selected" + (dups > 0 ? $" · {dups} already in library" : "");
        }
    }

    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event EventHandler<bool>? RequestClose;

    public IReadOnlyList<GameEntry> SelectedEntries =>
        Candidates.Where(c => c.IsSelected).Select(c => c.Entry).ToList();

    private void SetAll(bool value)
    {
        foreach (var c in Candidates)
            c.IsSelected = value;
    }
}
