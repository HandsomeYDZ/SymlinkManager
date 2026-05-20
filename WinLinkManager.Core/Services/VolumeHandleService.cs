using Microsoft.Win32.SafeHandles;
using WinLinkManager.Core.Native;

namespace WinLinkManager.Core.Services;

public class VolumeHandleService : IVolumeHandleService
{
    private SafeFileHandle? _volumeHandle;

    public SafeHandleWrapper Wrapper { get; } = new();

    public void OpenVolume(string volumePath = @"\\.\C:")
    {
        _volumeHandle = NtfsNative.CreateFileW(
            volumePath,
            NtfsNative.GENERIC_READ,
            NtfsNative.FILE_SHARE_READ | NtfsNative.FILE_SHARE_WRITE | NtfsNative.FILE_SHARE_DELETE,
            IntPtr.Zero,
            NtfsNative.OPEN_EXISTING,
            NtfsNative.FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        Wrapper.Handle = _volumeHandle?.DangerousGetHandle() ?? IntPtr.Zero;
    }

    public SafeHandleWrapper GetHandle() => Wrapper;

    public void Dispose()
    {
        _volumeHandle?.Dispose();
        _volumeHandle = null;
        Wrapper.Handle = IntPtr.Zero;
    }
}
