using System.Windows.Input;

namespace WinLinkManager.App.ViewModels.Base;

/// <summary>
/// 实现 ICommand 的中继命令，支持参数化和条件执行。
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private EventHandler? _canExecuteChanged;

    /// <param name="execute">带参数的执行委托。</param>
    /// <param name="canExecute">可选的条件判断委托。</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary> 无参数构造，内部包装为带参委托。 </summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is not null ? _ => canExecute() : null)
    {
    }

    /// <summary> 订阅/取消订阅时同时关联 CommandManager 的全局刷新。 </summary>
    public event EventHandler? CanExecuteChanged
    {
        add { _canExecuteChanged += value; CommandManager.RequerySuggested += value; }
        remove { _canExecuteChanged -= value; CommandManager.RequerySuggested -= value; }
    }

    /// <summary> 判断命令当前是否可执行。 </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary> 执行命令。 </summary>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary> 手动触发 CanExecuteChanged，通知 UI 刷新命令状态。 </summary>
    public void RaiseCanExecuteChanged()
    {
        _canExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
