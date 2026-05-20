using System.Windows;

namespace WinLinkManager.App.Views;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog()
    {
        InitializeComponent();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
