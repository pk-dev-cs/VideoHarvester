using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace VideoHarvester.Models;

public class Video : INotifyPropertyChanged
{
    private int _progress;
    private string _status = "Queued";  // Default status
    private string? _errorMessage;
    private BitmapImage? _thumbnailImage;
    private string? _title;
    private string? _quality;

    public required string VideoId { get; set; }
    public required VideoSource Source { get; set; }
    public int Order { get; set; }

    public string? Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string? Quality
    {
        get => _quality;
        set
        {
            _quality = value;
            OnPropertyChanged(nameof(Quality));
        }
    }

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

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged(nameof(ErrorMessage));
        }
    }

    public BitmapImage? ThumbnailImage
    {
        get => _thumbnailImage;
        set
        {
            _thumbnailImage = value;
            OnPropertyChanged(nameof(ThumbnailImage));
        }
    }

    public string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), $"{Order}.{Source switch { VideoSource.YouTube => "mkv", VideoSource.YouTubeWav => "wav", _ => "mp4" }}");

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

