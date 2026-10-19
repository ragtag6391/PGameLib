using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PGameLib.ViewModels;

namespace PGameLib.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <summary>Opens the Import dropdown as a context menu anchored to the button.</summary>
    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>Double-clicking a card or row plays that game (if the click landed on an item).</summary>
    private void GamesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list || Vm is null)
            return;

        // Only act when the double-click actually hit a game item, not empty space.
        if (list.SelectedItem is GameCardViewModel card && Vm.PlayCommand.CanExecute(card))
            Vm.PlayCommand.Execute(card);
    }
}
