using System.ComponentModel;
using System.ComponentModel;
using System.IO;

namespace VideoHarvester;

public class Video : INotifyPropertyChanged
{
    private int _progress;
    private string _status = "Queued";  // Default status

    public required string VideoId { get; set; }
    public int Order { get; set; }

    public int Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            OnPropertyChanged(nameof(Progress));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), $"{Order}.mp4");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

