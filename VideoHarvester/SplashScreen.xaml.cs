using System.Windows;
using System.Windows.Threading;

namespace VideoHarvester
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        private DispatcherTimer _timer;

        public SplashScreen()
        {
            InitializeComponent();

            // Set up timer to close splash screen after a delay
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5); // Show splash for 5 seconds
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;

            // Close the splash screen
            Close();
        }
    }
}
