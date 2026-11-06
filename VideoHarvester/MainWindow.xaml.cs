using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Media.Animation;
using VideoHarvester.Messages;
using VideoHarvester.Services;
using VideoHarvester.Views;
using WpfApplication = System.Windows.Application;

namespace VideoHarvester;

public partial class MainWindow : Window
{
    private bool _isMenuOpen = false;
    private AppUpdateWindow? _updateWindow;

    public AppUpdatesViewModel Updates { get; }

    public MainWindow()
    {
        var viewModel = App.Current.Services.GetRequiredService<MainWindowViewModel>();
        Updates = new AppUpdatesViewModel(App.Current.Services.GetRequiredService<IAppUpdateService>(), () => viewModel.CanRestartForUpdate);
        InitializeComponent();
        DataContext = viewModel;
        Updates.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AppUpdatesViewModel.IsPanelVisible))
                ShowOrHideUpdateWindow();
        };
        Loaded += async (_, _) => await Updates.CheckOnStartupAsync();

        WeakReferenceMessenger.Default.Register<FolderOpenedMessage>(this, (r, message) =>
        {
            WpfApplication.Current.Dispatcher.Invoke(() => VideoIdTextBox.Clear());
        });
    }

    private void ShowOrHideUpdateWindow()
    {
        if (!Updates.IsPanelVisible)
        {
            _updateWindow?.Close();
            return;
        }

        if (_updateWindow is not null)
        {
            _updateWindow.Activate();
            return;
        }

        _updateWindow = new AppUpdateWindow { Owner = this, DataContext = Updates };
        _updateWindow.Closed += (_, _) =>
        {
            _updateWindow = null;
            Updates.IsPanelVisible = false;
        };
        _updateWindow.Show();
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
            viewModel.IsSettingsView = false;
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
            viewModel.IsSettingsView = false;
            _ = viewModel.LoadHistoryCommand.ExecuteAsync(null);
        }
        CloseMenu();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel != null)
        {
            viewModel.IsQueueView = false;
            viewModel.IsSettingsView = true;
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
