using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PGameLib.ViewModels;

/// <summary>
/// Minimal INotifyPropertyChanged base. Hand-rolled so the project needs no MVVM NuGet package.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Sets the backing field and raises PropertyChanged only when the value actually changed.
    /// Returns true when a change occurred.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
