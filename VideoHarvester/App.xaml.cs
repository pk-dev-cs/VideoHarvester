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
            services.AddSingleton<MainWindowViewModel, MainWindowViewModel>();

            return services.BuildServiceProvider();
        }
    }
}
