using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VideoHarvester
{
    public partial class MainWindow : Window
    {
        private static int Order = 0;
        public ObservableCollection<Video> DownloadQueue { get; } = new();

        private bool isDownloading = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;  // Ensure UI binding
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string filePath)
            {
                string? folderPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
            }
        }

        private void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            string videoId = VideoIdTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(videoId))
            {
                MessageBox.Show("Please enter a valid VIDEO-ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var video = new Video { Order = ++Order, VideoId = videoId, Progress = 0 };
            DownloadQueue.Add(video);

            VideoIdTextBox.Clear();

            if (!isDownloading)
            {
                ProcessQueue();
            }
        }

        private async void ProcessQueue()
        {
            if (isDownloading) return; // Prevent multiple executions

            isDownloading = true;

            while (DownloadQueue.Any(v => v.Status == "Queued")) // Process until all are downloaded
            {
                var video = DownloadQueue.First(v => v.Status == "Queued"); // Get the first queued video
                await DownloadVideo(video); // Download and update UI
            }

            isDownloading = false;
        }

        private async Task DownloadVideo(Video video)
        {
            string apiUrl = $"http://fast.wistia.net/embed/iframe/{video.VideoId}";

            using HttpClient client = new();
            client.Timeout = TimeSpan.FromMinutes(10);

            try
            {
                video.Status = "Downloading";

                string response = await client.GetStringAsync(apiUrl);
                Match match = Regex.Match(response, "W\\.iframeInit\\((\\{.*?\\}), \\{");
                if (!match.Success)
                {
                    video.Status = "Failed";
                    return;
                }

                string jsonString = match.Groups[1].Value;
                using JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
                string? videoUrl = jsonDocument.RootElement.GetProperty("assets")[0].GetProperty("url").GetString();

                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    video.Status = "Failed";
                    return;
                }

                videoUrl = videoUrl.Replace(".bin", ".mp4");

                string outputFile = video.FilePath;

                using HttpResponseMessage videoResponse = await client.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead);
                using Stream videoStream = await videoResponse.Content.ReadAsStreamAsync();
                using FileStream fileStream = new(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[8192];
                long totalBytes = videoResponse.Content.Headers.ContentLength ?? -1;
                long receivedBytes = 0;
                int bytesRead;

                while ((bytesRead = await videoStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    receivedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        video.Progress = (int)((receivedBytes * 100) / totalBytes);
                    }
                }

                video.Progress = 100;
                video.Status = "Downloaded"; // Now marked as Downloaded!
            }
            catch
            {
                video.Status = "Failed";
            }
        }

        private void MinimizeApp_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreApp_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                var workArea = SystemParameters.WorkArea;

                this.WindowState = WindowState.Normal; // Ensure it's reset first
                this.Top = workArea.Top;
                this.Left = workArea.Left;
                this.Width = workArea.Width;
                this.Height = workArea.Height;
            }
            else
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
