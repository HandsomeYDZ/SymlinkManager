using WinLinkManager.App.ViewModels.Base;
using WinLinkManager.Core.Models;

namespace WinLinkManager.App.ViewModels;

/// <summary>
/// 链接类型转换预览对话框的 ViewModel，展示转换前后的类型对比。
/// </summary>
public class ConversionPreviewViewModel : ViewModelBase
{
    /// <summary> 待转换的链接条目。 </summary>
    public LinkEntry? Entry { get; set; }
    /// <summary> 目标链接类型。 </summary>
    public LinkType NewType { get; set; }

    /// <summary> 当前类型的显示文本。 </summary>
    public string CurrentTypeDisplay => Entry?.LinkTypeDisplay ?? "";
    /// <summary> 新类型的显示文本。 </summary>
    public string NewTypeDisplay => NewType switch
    {
        LinkType.Junction => "交接点(/J)",
        LinkType.DirectoryLink => "目录符号链接(/D)",
        _ => "未知"
    };

    /// <summary> 转换操作的描述文本。 </summary>
    public string Description => Entry == null ? "" :
        $"将「{Entry.LinkName}」从 {CurrentTypeDisplay} 转换为 {NewTypeDisplay}";

    public ConversionPreviewViewModel() { }
}
