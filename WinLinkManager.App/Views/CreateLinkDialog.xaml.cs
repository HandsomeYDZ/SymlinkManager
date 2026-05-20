using System.Windows;
using WinLinkManager.App.ViewModels;

namespace WinLinkManager.App.Views;

public partial class CreateLinkDialog : Window
{
    public CreateLinkDialog()
    {
        InitializeComponent();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CreateLinkViewModel vm) return;
        TryConfirm(vm);
    }

    private void TryConfirm(CreateLinkViewModel vm)
    {
        if (vm.Confirm())
        {
            DialogResult = true;
            Close();
            return;
        }

        // 目标路径不存在时询问用户
        if (vm.TargetNotFound)
        {
            var choice = MessageBox.Show(
                $"目标路径不存在:\n{vm.TargetPath}\n\n是否创建该目录?",
                "目标路径不存在",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (choice)
            {
                case MessageBoxResult.Yes:
                    vm.CreateTargetAndRetry();
                    TryConfirm(vm);
                    break;
                case MessageBoxResult.No:
                    vm.SkipTargetCheckAndRetry();
                    TryConfirm(vm);
                    break;
                // Cancel → 不重试，让用户修改或取消
            }
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
