using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using VideoHarvester.Services;

namespace VideoHarvester
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        public new static App Current => (App)Application.Current;

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();


            services.AddSingleton<IDownloadVideoService, DownloadVideoService>();
            services.AddSingleton<IDownloadHistoryService, DownloadHistoryService>();
            services.AddSingleton<MainWindowViewModel, MainWindowViewModel>();

            return services.BuildServiceProvider();
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // Initialize database
            var historyService = Services.GetRequiredService<IDownloadHistoryService>();
            await historyService.InitializeDatabaseAsync();

            // Create and show main window first (but don't show it yet)
            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;

            // Show splash screen
            var splashScreen = new SplashScreen();
            splashScreen.Show();

            // After splash screen closes, show main window
            splashScreen.Closed += (s, args) =>
            {
                mainWindow.Show();
            };
        }
    }
}
