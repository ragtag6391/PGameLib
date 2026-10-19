using System.Windows;
using System.Windows.Threading;
using PGameLib.Services;
using PGameLib.Services.Import;
using PGameLib.ViewModels;
using PGameLib.Views;

namespace PGameLib;

/// <summary>
/// Application entry point. Wires up the services and shows the main window.
/// A tiny amount of manual composition is used instead of a DI container to keep the
/// dependency surface small (no extra NuGet packages required).
/// </summary>
public partial class App : Application
{
    public static ILibraryStore LibraryStore { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // All persistent data lives under %AppData%\PGameLib.
        LibraryStore = new JsonLibraryStore();

        var mainViewModel = new MainViewModel(
            LibraryStore,
            new FolderSizeService(),
            new IconExtractor(),
            new GameLauncher(),
            new ImportCoordinator());

        var window = new MainWindow { DataContext = mainViewModel };
        MainWindow = window;

        // The dialog service needs the window as its owner; attach it before any dialogs can fire.
        mainViewModel.AttachDialogService(new DialogService(window));

        window.Show();

        // Kick off the initial load (and first-run preload seeding) without blocking the UI thread.
        _ = mainViewModel.InitializeAsync();
    }

    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Last-resort guard: never let an unexpected error crash the whole app silently.
        MessageBox.Show(
            "Something went wrong:\n\n" + e.Exception.Message,
            "PGameLib",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
