using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Media.Animation;
using VideoHarvester.Messages;

namespace VideoHarvester;

public partial class MainWindow : Window
{
    private bool _isMenuOpen = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetService<MainWindowViewModel>();

        WeakReferenceMessenger.Default.Register<FolderOpenedMessage>(this, (r, message) =>
        {
            Application.Current.Dispatcher.Invoke(() => VideoIdTextBox.Clear());
        });
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        OpenMenu();
    }

    private void CloseMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
    }

    private void MenuOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CloseMenu();
    }

    private void QueueListButton_Click(object sender, RoutedEventArgs e)
    {
        // Show Queue List view (current view)
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel != null)
        {
            viewModel.IsQueueView = true;
        }
        CloseMenu();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        // Show Download History view
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel != null)
        {
            viewModel.IsQueueView = false;
            _ = viewModel.LoadHistoryCommand.ExecuteAsync(null);
        }
        CloseMenu();
    }

    private void OpenMenu()
    {
        _isMenuOpen = true;

        var menuAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var overlayAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300)
        };

        MenuPanel.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, menuAnimation);
        MenuOverlay.BeginAnimation(OpacityProperty, overlayAnimation);
        MenuOverlay.IsHitTestVisible = true;
    }

    private void CloseMenu()
    {
        _isMenuOpen = false;

        var menuAnimation = new DoubleAnimation
        {
            To = 320,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        var overlayAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300)
        };

        MenuPanel.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, menuAnimation);
        MenuOverlay.BeginAnimation(OpacityProperty, overlayAnimation);
        MenuOverlay.IsHitTestVisible = false;
    }
}
