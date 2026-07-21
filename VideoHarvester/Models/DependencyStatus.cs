using System.ComponentModel;

namespace VideoHarvester.Models;

public class DependencyStatus : INotifyPropertyChanged
{
    private bool _isAvailable;

    public required string Name { get; init; }
    public required string Icon { get; init; }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            _isAvailable = value;
            OnPropertyChanged(nameof(IsAvailable));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
