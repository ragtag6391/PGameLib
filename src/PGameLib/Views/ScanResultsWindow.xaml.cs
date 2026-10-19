using System.Windows;
using PGameLib.ViewModels;

namespace PGameLib.Views;

public partial class ScanResultsWindow : Window
{
    private ScanResultsViewModel? _vm;

    public ScanResultsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.RequestClose -= OnRequestClose;

        _vm = e.NewValue as ScanResultsViewModel;

        if (_vm is not null)
            _vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(object? sender, bool result) => DialogResult = result;
}
