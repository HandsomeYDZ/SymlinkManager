using System.Windows;
using WinLinkManager.App.ViewModels;

namespace WinLinkManager.App;

public partial class MainWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
