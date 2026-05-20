using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinLinkManager.Core.Models;

/// <summary>
/// 符号链接条目模型，支持 WPF 属性变更通知
/// </summary>
public class LinkEntry : INotifyPropertyChanged
{
    private string _linkPath = string.Empty;
    private string _linkName = string.Empty;
    private string _targetPath = string.Empty;
    private LinkType _linkType;
    private string _creationTime = string.Empty;
    private LinkStatus _status = LinkStatus.Valid;
    private bool _inWhitelist;
    private string _lastSeenTime = string.Empty;

    public string LinkPath { get => _linkPath; set => SetProperty(ref _linkPath, value); }
    public string LinkName { get => _linkName; set => SetProperty(ref _linkName, value); }
    public string TargetPath { get => _targetPath; set => SetProperty(ref _targetPath, value); }
    public LinkType LinkType
    {
        get => _linkType;
        set { if (SetProperty(ref _linkType, value)) OnPropertyChanged(nameof(LinkTypeDisplay)); }
    }
    public string CreationTime { get => _creationTime; set => SetProperty(ref _creationTime, value); }
    public LinkStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
    public bool InWhitelist { get => _inWhitelist; set => SetProperty(ref _inWhitelist, value); }
    public string LastSeenTime { get => _lastSeenTime; set => SetProperty(ref _lastSeenTime, value); }

    /// <summary>链接类型的本地化显示名称</summary>
    public string LinkTypeDisplay => LinkType switch
    {
        LinkType.FileLink => "文件符号链接",
        LinkType.DirectoryLink => "目录符号链接(/D)",
        LinkType.Junction => "交接点(/J)",
        _ => "未知"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>触发属性变更通知</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>设置字段值并在变化时通知 UI</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
