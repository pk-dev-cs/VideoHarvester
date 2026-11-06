using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoHarvester;
using VideoHarvester.Services;
using VideoHarvester.Views;

internal static class UpdateLayoutChecks
{
    public static void Run(string? screenshotDirectory)
    {
        var app = new Application();
        try
        {
            LocalizationChecks.RunBindings();
            foreach (bool downloaded in new[] { false, true })
            {
                var model = new AppUpdatesViewModel(new UnusedUpdates(), () => false)
                {
                    AvailableVersion = "0.4.0",
                    IsDownloaded = downloaded,
                    IsPanelVisible = true,
                    Status = downloaded
                        ? "Finish or remove all unfinished queue items before updating. The update is ready when you are."
                        : "Version 0.4.0 is available."
                };
                var control = new UpdateNotification { DataContext = model, Width = 428 };
                control.Measure(new Size(428, double.PositiveInfinity));
                control.Arrange(new Rect(new Point(), control.DesiredSize));
                control.UpdateLayout();
                var buttons = Descendants(control).OfType<Button>().Where(button => button.Visibility == Visibility.Visible).ToList();
                if (buttons.Count != 3 || buttons.Any(button => button.ActualWidth <= 0 || button.TranslatePoint(new Point(button.ActualWidth, 0), control).X > control.ActualWidth + 1))
                    throw new InvalidOperationException("Update actions must remain visible and fit the update dialog width.");

                var action = buttons[0];
                if (!ReferenceEquals(action.Command, downloaded ? model.InstallUpdateCommand : model.DownloadUpdateCommand))
                    throw new InvalidOperationException("Update actions must bind to the displayed model.");

                if (screenshotDirectory is not null)
                {
                    Directory.CreateDirectory(screenshotDirectory);
                    var drawing = new DrawingVisual();
                    using (var context = drawing.RenderOpen())
                    {
                        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(21, 25, 34)), null, new Rect(0, 0, control.ActualWidth, control.ActualHeight));
                        context.DrawRectangle(new VisualBrush(control), null, new Rect(0, 0, control.ActualWidth, control.ActualHeight));
                    }
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(control.ActualWidth), (int)Math.Ceiling(control.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(drawing);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(screenshotDirectory, downloaded ? "update-ready.png" : "update-available.png"));
                    encoder.Save(file);
                }

                model.LaterCommand.Execute(null);
                control.UpdateLayout();
            }
            Console.WriteLine("PASS update-layout-and-bindings");
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private sealed class UnusedUpdates : IAppUpdateService
    {
        public bool IsInstalled => true;
        public bool HasDownloadedUpdate => false;
        public Task<string?> CheckAsync() => throw new NotSupportedException();
        public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void ApplyAndRestart() => throw new NotSupportedException();
    }
}
