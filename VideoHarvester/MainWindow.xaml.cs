using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;

namespace VideoHarvester
{
    public class Video
    {
        public string VideoId { get; set; }
        public int Order { get; set; }
    }

    public partial class MainWindow : Window
    {
        private static int Order = 0;
        private Queue<Video> downloadQueue = new Queue<Video>();
        private bool isDownloading = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            string videoId = VideoIdTextBox.Text.Trim();
            if (string.IsNullOrEmpty(videoId))
            {
                MessageBox.Show("Please enter a valid VIDEO-ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var video = new Video { Order = ++Order, VideoId = videoId };
            downloadQueue.Enqueue(video);
            DownloadQueueListBox.Items.Add(video);
            VideoIdTextBox.Clear();

            if (!isDownloading)
            {
                ProcessQueue();
            }
        }

        private async void ProcessQueue()
        {
            isDownloading = true;
            while (downloadQueue.Count > 0)
            {
                var video = downloadQueue.Dequeue();
                await DownloadVideo(video);
                DownloadQueueListBox.Items.Remove(video);
            }
            isDownloading = false;
        }

        private async Task DownloadVideo(Video video)
        {
            string apiUrl = $"http://fast.wistia.net/embed/iframe/{video.VideoId}";
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            try
            {
                string response = await client.GetStringAsync(apiUrl);
                Match match = Regex.Match(response, "W\\.iframeInit\\((\\{.*?\\}), \\{");
                if (!match.Success)
                {
                    MessageBox.Show($"Failed to extract video URL for {video.VideoId}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string jsonString = match.Groups[1].Value;
                using JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
                string videoUrl = jsonDocument.RootElement.GetProperty("assets")[0].GetProperty("url").GetString();
                videoUrl = videoUrl.Replace(".bin", ".mp4");

                string outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), $"{video.Order}.mp4");

                using HttpResponseMessage videoResponse = await client.GetAsync(videoUrl);
                using Stream videoStream = await videoResponse.Content.ReadAsStreamAsync();
                using FileStream fileStream = new(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await videoStream.CopyToAsync(fileStream);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading {video.VideoId}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
