using WinLinkManager.App.ViewModels.Base;
using WinLinkManager.Core.Models;

namespace WinLinkManager.App.ViewModels;

/// <summary>
/// 删除确认对话框的 ViewModel，显示待删除链接的信息和警告。
/// </summary>
public class DeleteConfirmViewModel : ViewModelBase
{
    /// <summary> 待删除的链接条目。 </summary>
    public LinkEntry? Entry { get; set; }

    /// <summary> 确认提示文本，包含链接路径和目标路径。 </summary>
    public string Message => Entry == null ? "" :
        $"确定要删除符号链接「{Entry.LinkName}」吗？\n\n路径: {Entry.LinkPath}\n目标: {Entry.TargetPath}\n\n此操作不可恢复。";

    public DeleteConfirmViewModel() { }
}
