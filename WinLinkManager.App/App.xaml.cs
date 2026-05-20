using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WinLinkManager.Core.Data;
using WinLinkManager.Core.Services;
using WinLinkManager.App.ViewModels;

namespace WinLinkManager.App;

/// <summary>
/// 应用程序入口，负责启动初始化、管理员权限检查和依赖注入。
/// </summary>
public partial class App : Application
{
    private readonly CancellationTokenSource _appCts = new();
    private ServiceProvider? _services;
    internal static ServiceProvider Services => ((App)Current)._services!;

    // 持久化目录：固定用 %LocalAppData%\WinLinkManager （开发/发布一致）
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLinkManager");
    public static string ConfigDir => Path.Combine(DataDir, "config");
    public static string ConfigFile => Path.Combine(ConfigDir, "app.config");

    public static string DefaultDatabasePath => Path.Combine(DataDir, "winlink-manager.db");
    public static string LogDir => Path.Combine(DataDir, "logs");

    /// <summary> 从文本文件加载配置（每行一个 Key=Value）。 </summary>
    public static AppConfig LoadConfig()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(ConfigFile))
            {
                var config = new AppConfig();
                foreach (var line in File.ReadAllLines(ConfigFile))
                {
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    if (key == "DatabasePath") config.DatabasePath = val;
                }
                return config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"读取配置文件失败: {ex.Message}");
        }
        return new AppConfig();
    }

    /// <summary> 将配置保存为简单的 Key=Value 文本文件。 </summary>
    public static void SaveConfig(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(config.DatabasePath))
            lines.Add($"DatabasePath={config.DatabasePath}");
        File.WriteAllLines(ConfigFile, lines);
    }

    /// <summary> 应用启动入口：检查管理员权限、配置日志、注册 DI 服务并显示主窗口。 </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 非管理员权限时弹窗提示重启
        if (!IsAdministrator())
        {
            var result = MessageBox.Show(
                "WinLinkManager 需要管理员权限才能扫描 NTFS 卷。\n\n是否以管理员身份重新启动？",
                "需要管理员权限",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                              ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
                              ?? "";
                var psi = new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                try { Process.Start(psi); } catch { }
            }
            Shutdown();
            return;
        }

        // 捕获 UI 线程未处理异常，弹窗提示后标记已处理防止崩溃
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show($"UI 异常: {ex.Exception.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        try
        {
            var config = LoadConfig();

            // 配置 Serilog 文件日志（每日滚动，保留 30 天）
            Directory.CreateDirectory(LogDir);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(LogDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .CreateLogger();

            // 注册依赖注入服务
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddSerilog(dispose: true));

            var dbPath = string.IsNullOrWhiteSpace(config.DatabasePath)
                ? DefaultDatabasePath
                : config.DatabasePath;
            services.AddSingleton(new AppDbContext($"Data Source={dbPath}"));

            services.AddSingleton<IIndexService, IndexService>();
            services.AddSingleton<ILinkService, LinkService>();
            services.AddSingleton<IWhitelistService, WhitelistService>();
            services.AddSingleton<IScannerService, MftScannerService>();
            services.AddSingleton<IUsnMonitorService, UsnMonitorService>();

            // ViewModel 和窗口注册
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            services.AddTransient<CreateLinkViewModel>();
            services.AddTransient<ConversionPreviewViewModel>();
            services.AddTransient<DeleteConfirmViewModel>();
            services.AddTransient<SettingsViewModel>(sp =>
                new SettingsViewModel(sp.GetRequiredService<IIndexService>()));

            _services = services.BuildServiceProvider();

            var mainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            _ = InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败: {ex}", "致命错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary> 后台初始化：数据库、索引、USN 监控，完成后通知主 ViewModel。 </summary>
    private async Task InitializeAsync()
    {
        if (_services == null) return;
        try
        {
            var logger = _services.GetRequiredService<ILogger<App>>();

            logger.LogInformation("数据目录: {Dir}", DataDir);
            logger.LogInformation("初始化数据库...");
            var db = _services.GetRequiredService<AppDbContext>();
            await db.InitializeAsync();

            logger.LogInformation("加载索引...");
            var idx = _services.GetRequiredService<IIndexService>();
            await idx.InitializeAsync();
            idx.StartBatchPersist(_appCts.Token);

            logger.LogInformation("加载扫描目录...");
            _ = await idx.GetScanDirectoriesAsync();

            logger.LogInformation("启动 USN 监控...");
            var usn = _services.GetRequiredService<IUsnMonitorService>();
            await usn.StartAsync(_appCts.Token);

            logger.LogInformation("初始化完成");

            await Dispatcher.InvokeAsync(async () =>
            {
                var mainVm = _services.GetRequiredService<MainViewModel>();
                await mainVm.InitializeAsync();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show($"初始化失败: {ex.Message}\n\n{ex.StackTrace}",
                    "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    /// <summary> 应用退出时清理资源：停止 USN 监控、释放 DI 容器、刷新日志。 </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        _appCts.Cancel();
        if (_services != null)
        {
            var usn = _services.GetRequiredService<IUsnMonitorService>();
            await usn.StopAsync();
        }
        _appCts.Dispose();
        _services?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary> 检查当前进程是否以管理员身份运行。 </summary>
    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

public class AppConfig
{
    public string? DatabasePath { get; set; }
}
