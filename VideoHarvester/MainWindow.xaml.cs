using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using VideoHarvester.Messages;

namespace VideoHarvester;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetService<MainWindowViewModel>();

        WeakReferenceMessenger.Default.Register<FolderOpenedMessage>(this, (r, message) =>
        {
            Application.Current.Dispatcher.Invoke(() => VideoIdTextBox.Clear());
        });
    }
}
