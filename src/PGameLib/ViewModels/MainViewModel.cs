using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using PGameLib.Helpers;
using PGameLib.Models;
using PGameLib.Services;
using PGameLib.Services.Import;

namespace PGameLib.ViewModels;

/// <summary>
/// The application's main view-model. Owns the library collection, the filtered/sorted view that
/// the UI binds to, and every command the window exposes. Long-running work (sizes, scans,
/// refresh) runs off the UI thread so the interface stays responsive.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ILibraryStore _store;
    private readonly FolderSizeService _sizeService;
    private readonly IconExtractor _iconExtractor;
    private readonly GameLauncher _launcher;
    private readonly ImportCoordinator _import;
    private readonly IDialogService _dialogs;

    private bool _preloadSeeded;
    private CancellationTokenSource? _backgroundCts;

    public MainViewModel(
        ILibraryStore store,
        FolderSizeService sizeService,
        IconExtractor iconExtractor,
        GameLauncher launcher,
        ImportCoordinator import,
        IDialogService? dialogs = null)
    {
        _store = store;
        _sizeService = sizeService;
        _iconExtractor = iconExtractor;
        _launcher = launcher;
        _import = import;
        // The dialog service is supplied by the view layer right after construction when null here.
        _dialogs = dialogs ?? NullDialogService.Instance;

        GamesView = CollectionViewSource.GetDefaultView(AllGames);
        GamesView.Filter = FilterGame;
        ApplySort();

        Settings = new SettingsViewModel(this);

        // Commands
        PlayCommand = new RelayCommand(p => Play(AsCard(p)), p => AsCard(p) is not null);
        OpenFolderCommand = new RelayCommand(p => OpenFolder(AsCard(p)), p => AsCard(p) is not null);
        EditCommand = new RelayCommand(p => Edit(AsCard(p)), p => AsCard(p) is not null);
        RemoveCommand = new RelayCommand(p => Remove(AsCard(p)), p => AsCard(p) is not null);
        LocateCommand = new RelayCommand(p => Locate(AsCard(p)), p => AsCard(p) is not null);
        AddGameCommand = new RelayCommand(_ => AddGame());
        SelectFilterCommand = new RelayCommand(p => SelectFilter(p?.ToString()));
        SetGridViewCommand = new RelayCommand(_ => ViewMode = Models.ViewMode.Grid);
        SetListViewCommand = new RelayCommand(_ => ViewMode = Models.ViewMode.List);

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        ImportSteamCommand = new AsyncRelayCommand(_ => ImportFromAsync("Steam games", _import.ScanSteamAsync));
        ImportEpicCommand = new AsyncRelayCommand(_ => ImportFromAsync("Epic games", _import.ScanEpicAsync));
        ImportStartMenuCommand = new AsyncRelayCommand(_ => ImportFromAsync("Start Menu shortcuts", _import.ScanStartMenuAsync));
        ImportAllCommand = new AsyncRelayCommand(_ => ImportFromAsync("all sources", _import.ScanAllSourcesAsync));
        ScanFolderCommand = new AsyncRelayCommand(_ => ScanFolderAsync());
    }

    /// <summary>Called by the view layer once the real dialog service exists.</summary>
    public void AttachDialogService(IDialogService dialogs) => _dialogsOverride = dialogs;
    private IDialogService? _dialogsOverride;
    private IDialogService Dialogs => _dialogsOverride ?? _dialogs;

    // ---- Collections / view ----

    public ObservableCollection<GameCardViewModel> AllGames { get; } = new();
    public ICollectionView GamesView { get; }
    public SettingsViewModel Settings { get; }

    // ---- Bindable state ----

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) GamesView.Refresh(); }
    }

    private LibraryFilter _currentFilter = LibraryFilter.All;
    public LibraryFilter CurrentFilter
    {
        get => _currentFilter;
        set
        {
            if (SetProperty(ref _currentFilter, value))
            {
                OnPropertyChanged(nameof(IsSettingsVisible));
                OnPropertyChanged(nameof(IsLibraryVisible));
                OnPropertyChanged(nameof(HeaderText));
                GamesView.Refresh();
                OnPropertyChanged(nameof(VisibleCountText));
            }
        }
    }

    public bool IsSettingsVisible => CurrentFilter == LibraryFilter.Settings;
    public bool IsLibraryVisible => CurrentFilter != LibraryFilter.Settings;

    public string HeaderText => CurrentFilter switch
    {
        LibraryFilter.Steam => "Steam",
        LibraryFilter.Epic => "Epic",
        LibraryFilter.Standalone => "Standalone",
        LibraryFilter.Missing => "Missing games",
        LibraryFilter.Settings => "Settings",
        _ => "Library"
    };

    private ViewMode _viewMode = Models.ViewMode.Grid;
    public ViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (SetProperty(ref _viewMode, value))
            {
                OnPropertyChanged(nameof(IsGridView));
                OnPropertyChanged(nameof(IsListView));
            }
        }
    }
    public bool IsGridView => ViewMode == Models.ViewMode.Grid;
    public bool IsListView => ViewMode == Models.ViewMode.List;

    private SortField _sortField = Models.SortField.Name;
    public SortField SortField
    {
        get => _sortField;
        set { if (SetProperty(ref _sortField, value)) ApplySort(); }
    }

    public IReadOnlyList<SortField> SortFields { get; } =
        new[] { SortField.Name, SortField.SizeBytes, SortField.LastPlayed, SortField.AddedDate, SortField.Source };

    private GameCardViewModel? _selectedGame;
    public GameCardViewModel? SelectedGame
    {
        get => _selectedGame;
        set { if (SetProperty(ref _selectedGame, value)) OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => SelectedGame is not null;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(IsNotBusy)); }
    }
    public bool IsNotBusy => !_isBusy;

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private long _combinedSizeBytes;
    public long CombinedSizeBytes
    {
        get => _combinedSizeBytes;
        private set { if (SetProperty(ref _combinedSizeBytes, value)) OnPropertyChanged(nameof(CombinedSizeText)); }
    }
    public string CombinedSizeText => SizeFormatter.Format(_combinedSizeBytes);

    public string GameCountText => AllGames.Count == 1 ? "1 game" : $"{AllGames.Count} games";

    public bool AnyGames => AllGames.Count > 0;

    public string VisibleCountText
    {
        get
        {
            var n = GamesView.Cast<object>().Count();
            return n == 1 ? "1 game" : $"{n} games";
        }
    }

    // ---- Commands ----

    public RelayCommand PlayCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand LocateCommand { get; }
    public RelayCommand AddGameCommand { get; }
    public RelayCommand SelectFilterCommand { get; }
    public RelayCommand SetGridViewCommand { get; }
    public RelayCommand SetListViewCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ImportSteamCommand { get; }
    public AsyncRelayCommand ImportEpicCommand { get; }
    public AsyncRelayCommand ImportStartMenuCommand { get; }
    public AsyncRelayCommand ImportAllCommand { get; }
    public AsyncRelayCommand ScanFolderCommand { get; }

    // ---- Lifecycle ----

    public async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "Loading library…";
            var data = await _store.LoadAsync();
            _preloadSeeded = data.PreloadSeeded;

            foreach (var entry in data.Games)
            {
                entry.IsAvailable = LaunchInspector.IsAvailable(entry);
                AllGames.Add(new GameCardViewModel(entry));
            }

            RecalculateCombinedSize();
            OnPropertyChanged(nameof(GameCountText));
            OnPropertyChanged(nameof(VisibleCountText));

            if (!_preloadSeeded)
            {
                StatusMessage = "Looking for installed games…";
                await SeedPreloadAsync();
            }

            StatusMessage = "Ready";

            // Background: fill in missing icons and sizes without blocking.
            _backgroundCts = new CancellationTokenSource();
            _ = Task.Run(() => EnsureIconsAndSizesAsync(AllGames.ToList(), force: false, _backgroundCts.Token));
        }
        catch (Exception ex)
        {
            Dialogs.ShowError("Couldn't load your library:\n" + ex.Message);
        }
    }

    private async Task SeedPreloadAsync()
    {
        try
        {
            var preload = await _import.BuildPreloadEntriesAsync();
            var existing = ExistingKeys();
            var added = 0;
            foreach (var entry in ImportCoordinator.DedupeWithin(preload))
            {
                if (existing.Contains(entry.NormalizedKey))
                    continue;
                entry.IsAvailable = LaunchInspector.IsAvailable(entry);
                AllGames.Add(new GameCardViewModel(entry));
                existing.Add(entry.NormalizedKey);
                added++;
            }

            _preloadSeeded = true;
            RecalculateCombinedSize();
            OnPropertyChanged(nameof(GameCountText));
            OnPropertyChanged(nameof(VisibleCountText));
            await SaveAsync();

            StatusMessage = added > 0 ? $"Added {added} game(s) found on this PC" : "Ready";
        }
        catch
        {
            // First-run seeding is best-effort; mark it done so it doesn't retry forever.
            _preloadSeeded = true;
            await SaveAsync();
        }
    }

    // ---- Core actions ----

    private void Play(GameCardViewModel? card)
    {
        if (card is null) return;
        var result = _launcher.Launch(card.Model);
        if (!result.Success)
        {
            Dialogs.ShowError(result.ErrorMessage ?? "The game couldn't be launched.");
        }
        else
        {
            StatusMessage = $"Launched {card.Name}";
            card.RaiseAllChanged();
            _ = SaveAsync();
        }
    }

    private void OpenFolder(GameCardViewModel? card)
    {
        if (card is null) return;
        var result = _launcher.OpenFolder(card.Model);
        if (!result.Success)
            Dialogs.ShowError(result.ErrorMessage ?? "The folder couldn't be opened.");
    }

    private void AddGame()
    {
        var vm = new AddEditGameViewModel(Dialogs, _import.Factory);
        if (!Dialogs.ShowAddEditDialog(vm))
            return;

        var entry = vm.BuildNewEntry();

        if (IsDuplicate(entry.NormalizedKey, excludeId: null))
        {
            Dialogs.ShowError($"\"{entry.Name}\" looks like it's already in your library (same launch file).");
            return;
        }

        entry.IsAvailable = LaunchInspector.IsAvailable(entry);
        var card = new GameCardViewModel(entry);
        AllGames.Add(card);
        OnPropertyChanged(nameof(GameCountText));
        OnPropertyChanged(nameof(VisibleCountText));
        GamesView.Refresh();

        _ = Task.Run(() => EnsureIconsAndSizesAsync(new[] { card }, force: true, CancellationToken.None));
        _ = SaveAsync();
        StatusMessage = $"Added {entry.Name}";
    }

    private void Edit(GameCardViewModel? card)
    {
        if (card is null) return;

        var previousKey = card.Model.NormalizedKey;
        var vm = new AddEditGameViewModel(Dialogs, _import.Factory, card.Model);
        if (!Dialogs.ShowAddEditDialog(vm))
            return;

        // Preview the new key for duplicate detection before committing.
        var preview = card.Model.Clone();
        vm.ApplyTo(preview);
        if (!PathNormalizer.AreSame(previousKey, preview.NormalizedKey) &&
            IsDuplicate(preview.NormalizedKey, excludeId: card.Id))
        {
            Dialogs.ShowError("Another entry already uses that launch file.");
            return;
        }

        var launchChanged = !string.Equals(card.Model.LaunchPath, preview.LaunchPath, StringComparison.Ordinal);
        vm.ApplyTo(card.Model);
        card.Model.IsAvailable = LaunchInspector.IsAvailable(card.Model);

        if (launchChanged)
        {
            // Path changed — the cached icon no longer matches, so re-extract.
            card.Model.IconCachePath = null;
            _ = Task.Run(() => EnsureIconsAndSizesAsync(new[] { card }, force: true, CancellationToken.None));
        }

        card.RaiseAllChanged();
        GamesView.Refresh();
        _ = SaveAsync();
        StatusMessage = $"Updated {card.Name}";
    }

    private void Remove(GameCardViewModel? card)
    {
        if (card is null) return;

        if (!Dialogs.Confirm(
                $"Remove \"{card.Name}\" from your library?\n\n" +
                "This only removes the entry here. The actual game and its files are left untouched."))
            return;

        AllGames.Remove(card);
        if (SelectedGame == card) SelectedGame = null;
        RecalculateCombinedSize();
        OnPropertyChanged(nameof(GameCountText));
        OnPropertyChanged(nameof(VisibleCountText));
        _ = SaveAsync();
        StatusMessage = $"Removed {card.Name}";
    }

    private void Locate(GameCardViewModel? card)
    {
        if (card is null) return;

        var picked = Dialogs.PickLaunchFile(
            System.IO.Directory.Exists(card.Model.InstallFolder) ? card.Model.InstallFolder : null);
        if (picked is null)
            return;

        // Rebuild folder/args from the new file, keeping the user's chosen name and source.
        var rebuilt = _import.Factory.FromLaunchFile(picked, card.Model.Source);

        if (IsDuplicate(rebuilt.NormalizedKey, excludeId: card.Id))
        {
            Dialogs.ShowError("Another entry already uses that launch file.");
            return;
        }

        card.Model.LaunchPath = rebuilt.LaunchPath;
        if (!string.IsNullOrWhiteSpace(rebuilt.InstallFolder))
            card.Model.InstallFolder = rebuilt.InstallFolder;
        card.Model.IconCachePath = null;
        card.Model.IsAvailable = LaunchInspector.IsAvailable(card.Model);

        _ = Task.Run(() => EnsureIconsAndSizesAsync(new[] { card }, force: true, CancellationToken.None));
        card.RaiseAllChanged();
        GamesView.Refresh();
        _ = SaveAsync();
        StatusMessage = $"Relinked {card.Name}";
    }

    // ---- Refresh ----

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Refreshing…";
        try
        {
            // Re-check availability on the UI thread (cheap), then recompute sizes in the background.
            foreach (var card in AllGames)
                card.IsAvailable = LaunchInspector.IsAvailable(card.Model);
            GamesView.Refresh();

            var cards = AllGames.ToList();
            await Task.Run(() => EnsureIconsAndSizesAsync(cards, force: true, CancellationToken.None));

            StatusMessage = "Refresh complete";
        }
        catch (Exception ex)
        {
            Dialogs.ShowError("Refresh ran into a problem:\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Import flows ----

    private async Task ImportFromAsync(string label, Func<Task<IReadOnlyList<GameEntry>>> scan)
    {
        IsBusy = true;
        StatusMessage = $"Scanning {label}…";
        try
        {
            var found = await scan();
            await PresentAndImportAsync($"Found {found.Count} {label}", found);
        }
        catch (Exception ex)
        {
            Dialogs.ShowError($"Couldn't scan {label}:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            StatusMessage = "Ready";
        }
    }

    private async Task ScanFolderAsync()
    {
        var folder = Dialogs.PickFolder("Select a folder or drive to scan for games");
        if (folder is null)
            return;

        IsBusy = true;
        using var cts = new CancellationTokenSource();
        var progress = new Progress<string>(p => StatusMessage = "Scanning: " + Shorten(p));
        try
        {
            var found = await _import.ScanFolderAsync(folder, progress, cts.Token);
            await PresentAndImportAsync($"Found {found.Count} possible game(s) in {folder}", found);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled";
        }
        catch (Exception ex)
        {
            Dialogs.ShowError("The folder scan ran into a problem:\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
            StatusMessage = "Ready";
        }
    }

    private async Task PresentAndImportAsync(string heading, IReadOnlyList<GameEntry> found)
    {
        if (found.Count == 0)
        {
            Dialogs.ShowInfo("No games were found.");
            return;
        }

        var reviewVm = new ScanResultsViewModel(heading, found, ExistingKeys());
        if (!Dialogs.ShowScanResultsDialog(reviewVm))
            return;

        var existing = ExistingKeys();
        var newCards = new List<GameCardViewModel>();
        foreach (var entry in reviewVm.SelectedEntries)
        {
            if (existing.Contains(entry.NormalizedKey))
                continue; // skip anything that duplicates the library or an earlier pick
            entry.IsAvailable = LaunchInspector.IsAvailable(entry);
            var card = new GameCardViewModel(entry);
            AllGames.Add(card);
            newCards.Add(card);
            existing.Add(entry.NormalizedKey);
        }

        OnPropertyChanged(nameof(GameCountText));
        OnPropertyChanged(nameof(VisibleCountText));
        GamesView.Refresh();
        await SaveAsync();

        StatusMessage = $"Imported {newCards.Count} game(s)";
        _ = Task.Run(() => EnsureIconsAndSizesAsync(newCards, force: true, CancellationToken.None));
    }

    // ---- Background icon + size work ----

    private async Task EnsureIconsAndSizesAsync(IReadOnlyList<GameCardViewModel> cards, bool force, CancellationToken ct)
    {
        foreach (var card in cards)
        {
            if (ct.IsCancellationRequested) return;
            var model = card.Model;

            // Icon: only for file launches that exist, when we don't already have a cached icon.
            try
            {
                var needIcon = string.IsNullOrWhiteSpace(model.IconCachePath) ||
                               !System.IO.File.Exists(model.IconCachePath);
                if (needIcon && !LaunchInspector.IsUri(model.LaunchPath) && System.IO.File.Exists(model.LaunchPath))
                {
                    var icon = _iconExtractor.ExtractAndCache(model.LaunchPath);
                    if (icon is not null)
                    {
                        model.IconCachePath = icon;
                        RunOnUi(card.RaiseImageChanged);
                    }
                }
            }
            catch { /* icon failures are non-fatal */ }

            // Size: recompute when forced, or when we've never computed it.
            try
            {
                var needSize = force || model.SizeComputedUtc is null || model.SizeBytes == 0;
                if (needSize && !string.IsNullOrWhiteSpace(model.InstallFolder) && System.IO.Directory.Exists(model.InstallFolder))
                {
                    var size = _sizeService.Calculate(model.InstallFolder, ct);
                    model.SizeBytes = size;
                    model.SizeComputedUtc = DateTime.UtcNow;
                    RunOnUi(() =>
                    {
                        card.RaiseSizeChanged();
                        RecalculateCombinedSize();
                    });
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* size failures are non-fatal */ }
        }

        RunOnUi(RecalculateCombinedSize);
        await SaveAsync();
    }

    // ---- Helpers ----

    private void SelectFilter(string? name)
    {
        if (Enum.TryParse<LibraryFilter>(name, ignoreCase: true, out var f))
            CurrentFilter = f;
    }

    private bool FilterGame(object obj)
    {
        if (obj is not GameCardViewModel card)
            return false;

        // Source/status filter
        var passesFilter = CurrentFilter switch
        {
            LibraryFilter.Steam => card.Source == GameSource.Steam,
            LibraryFilter.Epic => card.Source == GameSource.Epic,
            LibraryFilter.Standalone => card.Source is GameSource.Standalone or GameSource.Custom,
            LibraryFilter.Missing => !card.IsAvailable,
            _ => true // All / Settings (Settings hides the list anyway)
        };
        if (!passesFilter)
            return false;

        // Text search over name, launch target and folder
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            return card.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (card.InstallFolder?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (card.LaunchPath?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }

    private void ApplySort()
    {
        GamesView.SortDescriptions.Clear();

        switch (SortField)
        {
            case SortField.SizeBytes:
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.SizeBytes), ListSortDirection.Descending));
                break;
            case SortField.LastPlayed:
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.LastPlayedUtc), ListSortDirection.Descending));
                break;
            case SortField.AddedDate:
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.AddedUtc), ListSortDirection.Descending));
                break;
            case SortField.Source:
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.SourceText), ListSortDirection.Ascending));
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.Name), ListSortDirection.Ascending));
                break;
            default:
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.Name), ListSortDirection.Ascending));
                break;
        }
    }

    private void RecalculateCombinedSize()
    {
        long total = 0;
        foreach (var c in AllGames)
            total += c.Model.SizeBytes;
        CombinedSizeBytes = total;
        OnPropertyChanged(nameof(VisibleCountText));
    }

    private HashSet<string> ExistingKeys()
        => new(AllGames.Select(c => c.Model.NormalizedKey), StringComparer.Ordinal);

    private bool IsDuplicate(string key, string? excludeId)
        => AllGames.Any(c => c.Id != excludeId &&
                             string.Equals(c.Model.NormalizedKey, key, StringComparison.Ordinal));

    private static GameCardViewModel? AsCard(object? p) => p as GameCardViewModel;

    private async Task SaveAsync()
    {
        try
        {
            var data = new LibraryData
            {
                PreloadSeeded = _preloadSeeded,
                Games = AllGames.Select(c => c.Model).ToList()
            };
            await _store.SaveAsync(data);
        }
        catch (Exception ex)
        {
            // Don't nag on every autosave; surface once to the status line.
            StatusMessage = "Couldn't save library: " + ex.Message;
        }
    }

    private static string Shorten(string path)
        => path.Length <= 60 ? path : "…" + path[^57..];

    private static void RunOnUi(Action action)
    {
        var app = Application.Current;
        if (app is null) { action(); return; }
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.Invoke(action);
    }
}
