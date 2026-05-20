using System.Collections.Generic;
using System.Windows.Controls;
using WinLinkManager.App.ViewModels;
using WinLinkManager.Core.Models;

namespace WinLinkManager.App;

/// <summary> 主窗口，负责链接列表展示并与 MainViewModel 绑定。 </summary>
public partial class MainWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary> 同步 DataGrid 选中项到 ViewModel 的 SelectedLinks 集合。 </summary>
    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is DataGrid grid)
        {
            var selected = new List<LinkEntry>();
            foreach (var item in grid.SelectedItems)
                if (item is LinkEntry entry) selected.Add(entry);
            vm.SelectedLinks = selected;
        }
    }
}
