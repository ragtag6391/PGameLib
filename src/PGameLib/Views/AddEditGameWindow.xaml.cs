using System.Windows;
using PGameLib.ViewModels;

namespace PGameLib.Views;

public partial class AddEditGameWindow : Window
{
    private AddEditGameViewModel? _vm;

    public AddEditGameWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.RequestClose -= OnRequestClose;

        _vm = e.NewValue as AddEditGameViewModel;

        if (_vm is not null)
            _vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(object? sender, bool result)
    {
        // Setting DialogResult closes a modally-shown window and returns the value to ShowDialog().
        DialogResult = result;
    }
}
