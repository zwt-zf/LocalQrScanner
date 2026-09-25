using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;
using Community.PowerToys.Run.Plugin.LocalQrScanner.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner;

public sealed class Main : IPlugin, ISettingProvider, IDisposable
{
    private const string RetentionDaysKey = "RetentionDays";
    private const string MaxHistoryItemsKey = "MaxHistoryItems";
    private const string SaveQrSnapshotsKey = "SaveQrSnapshots";

    private readonly HistoryStore historyStore;
    private readonly ScreenQrScanner scanner;
    private readonly CancellationTokenSource disposalTokenSource = new();
    private PluginInitContext? context;
    private volatile ScannerSettings settings = ScannerSettings.Default;
    private string iconPath = "Images/localqrscanner.dark.png";
    private int scanInProgress;
    private bool disposed;

    static Main()
    {
        PluginDependencyResolver.Initialize();
    }

    public Main()
        : this(new HistoryStore(), new ScreenQrScanner())
    {
    }

    internal Main(HistoryStore historyStore, ScreenQrScanner scanner)
    {
        this.historyStore = historyStore;
        this.scanner = scanner;
    }

    public static string PluginID => "788600460E974239A2DB0FAD861029B0";

    public string Name => "Local QR Scanner";

    public string Description => "离线框选屏幕区域并识别二维码，支持本地预览和扫描历史";

    public IEnumerable<PluginAdditionalOption> AdditionalOptions =>
    [
        new()
        {
            Key = RetentionDaysKey,
            DisplayLabel = "历史保留天数",
            DisplayDescription = "过期记录及其二维码局部截图会自动清理。",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            NumberValue = settings.RetentionDays,
            NumberBoxMin = 1,
            NumberBoxMax = 3650,
            NumberBoxSmallChange = 1,
            NumberBoxLargeChange = 30,
        },
        new()
        {
            Key = MaxHistoryItemsKey,
            DisplayLabel = "最多保留记录数",
            DisplayDescription = "达到上限时优先清理最早的记录。",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            NumberValue = settings.MaxHistoryItems,
            NumberBoxMin = 10,
            NumberBoxMax = 5000,
            NumberBoxSmallChange = 10,
            NumberBoxLargeChange = 100,
        },
        new()
        {
            Key = SaveQrSnapshotsKey,
            DisplayLabel = "保存二维码局部截图",
            DisplayDescription = "只保存二维码附近区域，用于历史预览；不会保存完整屏幕。",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
            Value = settings.SaveQrSnapshots,
        },
    ];

    public void Init(PluginInitContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        context.API.ThemeChanged += OnThemeChanged;
        UpdateIconPath(context.API.GetCurrentTheme());
    }

    public List<Result> Query(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var search = query.Search.Trim();
        var normalized = search.ToLowerInvariant();
        var results = new List<Result>();

        if (string.IsNullOrEmpty(search))
        {
            results.Add(CreateScreenScanResult());
            results.Add(CreateClipboardScanResult());
            results.Add(CreateHistoryPanelResult());
            results.Add(CreateSettingsResult());
            return results;
        }

        if (MatchesCommand(normalized, "scan", "扫描", "screen", "屏幕"))
        {
            results.Add(CreateScreenScanResult());
            return results;
        }

        if (MatchesCommand(normalized, "clipboard", "clip", "剪贴板"))
        {
            results.Add(CreateClipboardScanResult());
            return results;
        }

        if (MatchesCommand(normalized, "settings", "setting", "config", "设置"))
        {
            results.Add(CreateSettingsResult());
            return results;
        }

        if (HasCommandPrefix(search, "history", "历史"))
        {
            results.Add(CreateHistoryPanelResult(RemoveCommandPrefix(search, "history", "历史")));
            return results;
        }

        if (MatchesCommand(normalized, "clear", "清空"))
        {
            results.Add(CreateClearHistoryResult());
            return results;
        }

        return results;
    }

    public System.Windows.Controls.Control CreateSettingPanel() => throw new NotImplementedException();

    public void UpdateSettings(PowerLauncherPluginSettings powerLauncherSettings)
    {
        var options = powerLauncherSettings?.AdditionalOptions;
        var retentionDays = ReadNumberOption(options, RetentionDaysKey, ScannerSettings.Default.RetentionDays, 1, 3650);
        var maxHistoryItems = ReadNumberOption(options, MaxHistoryItemsKey, ScannerSettings.Default.MaxHistoryItems, 10, 5000);
        var saveSnapshots = options?.FirstOrDefault(option => option.Key == SaveQrSnapshotsKey)?.Value ?? ScannerSettings.Default.SaveQrSnapshots;
        settings = new ScannerSettings(retentionDays, maxHistoryItems, saveSnapshots);
        historyStore.ApplyRetention(settings);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposalTokenSource.Cancel();
        disposalTokenSource.Dispose();
        if (context?.API is not null)
        {
            context.API.ThemeChanged -= OnThemeChanged;
        }

        disposed = true;
        GC.SuppressFinalize(this);
    }

    private Result CreateScreenScanResult()
    {
        return new Result
        {
            IcoPath = iconPath,
            Title = scanInProgress == 0 ? "截图并识别选区内的二维码" : "正在截图识别…",
            SubTitle = "拖动鼠标框选区域，只在选区内本地识别；Esc 可取消",
            Score = 100,
            Action = _ => StartScreenScan(),
        };
    }

    private Result CreateClipboardScanResult()
    {
        return new Result
        {
            IcoPath = iconPath,
            Title = "识别剪贴板图片中的二维码",
            SubTitle = "适用于已经复制或截取到剪贴板的图片",
            Score = 90,
            Action = _ => StartClipboardScan(),
        };
    }

    private Result CreateHistoryPanelResult(string search = "")
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        return new Result
        {
            IcoPath = iconPath,
            Title = hasSearch ? $"在历史记录中搜索“{search}”" : "打开历史记录面板",
            SubTitle = "在独立主界面中搜索、预览、复制或删除扫描记录",
            Score = 80,
            Action = _ =>
            {
                HistoryWindow.ShowPanel(historyStore, () => settings, search);
                return true;
            },
        };
    }

    private Result CreateSettingsResult()
    {
        return new Result
        {
            IcoPath = iconPath,
            Title = "打开 Local QR Scanner 设置",
            SubTitle = "前往 PowerToys Run 插件设置，配置历史保留和二维码截图",
            Score = 70,
            Action = _ => OpenPowerToysSettings(),
        };
    }

    private Result CreateClearHistoryResult()
    {
        return new Result
        {
            IcoPath = iconPath,
            Title = "清空全部扫描历史",
            SubTitle = "同时删除 Local QR Scanner 保存的二维码局部截图",
            Action = _ =>
            {
                var answer = System.Windows.MessageBox.Show(
                    "确定要清空全部二维码扫描历史吗？",
                    Name,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (answer == System.Windows.MessageBoxResult.Yes)
                {
                    historyStore.Clear();
                    RefreshQuery("history");
                }

                return false;
            },
        };
    }

    private bool StartScreenScan()
    {
        if (!TryBeginScan())
        {
            return false;
        }

        _ = RunRegionScanAsync();
        return true;
    }

    private async Task RunRegionScanAsync()
    {
        try
        {
            await Task.Delay(350, disposalTokenSource.Token).ConfigureAwait(false);
            using var bitmap = await RegionCaptureForm.CaptureAsync(disposalTokenSource.Token).ConfigureAwait(false);
            if (bitmap is not null)
            {
                CompleteScan(scanner.ScanBitmap(bitmap, "区域截图", settings.SaveQrSnapshots));
            }
        }
        catch (OperationCanceledException)
        {
            // PowerToys is shutting down, reloading the plugin, or the selection was canceled.
        }
        catch (Exception ex)
        {
            Log.Exception("Region QR scan failed", ex, GetType());
            ShowMessage("截图识别失败", ex.Message, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            Interlocked.Exchange(ref scanInProgress, 0);
        }
    }

    private bool StartClipboardScan()
    {
        if (!TryBeginScan())
        {
            return false;
        }

        Bitmap? bitmap;
        try
        {
            bitmap = ScreenQrScanner.TryGetClipboardBitmap();
        }
        catch (ExternalException ex)
        {
            Interlocked.Exchange(ref scanInProgress, 0);
            ShowMessage("无法读取剪贴板", ex.Message, System.Windows.MessageBoxImage.Error);
            return false;
        }

        if (bitmap is null)
        {
            Interlocked.Exchange(ref scanInProgress, 0);
            ShowMessage("剪贴板中没有图片", "请先复制图片或使用 Windows 截图工具截取二维码。", System.Windows.MessageBoxImage.Information);
            return false;
        }

        _ = Task.Run(() =>
        {
            try
            {
                using (bitmap)
                {
                    CompleteScan(scanner.ScanBitmap(bitmap, "剪贴板", settings.SaveQrSnapshots));
                }
            }
            catch (Exception ex)
            {
                Log.Exception("Clipboard QR scan failed", ex, GetType());
                ShowMessage("识别失败", ex.Message, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                Interlocked.Exchange(ref scanInProgress, 0);
            }
        });
        return true;
    }

    private bool TryBeginScan()
    {
        if (Interlocked.CompareExchange(ref scanInProgress, 1, 0) == 0)
        {
            return true;
        }

        ShowMessage("扫描正在进行", "请稍候，完成后会自动弹出识别结果。", System.Windows.MessageBoxImage.Information);
        return false;
    }

    private void CompleteScan(ScanReport report)
    {
        if (report.Detections.Count > 0)
        {
            var records = historyStore.AddRange(report.Detections, settings);
            ScanResultWindow.ShowResults(
                records,
                () => HistoryWindow.ShowPanel(historyStore, () => settings));
        }
        else if (report.ImagesScanned == 0 && report.Errors.Count > 0)
        {
            ShowMessage("无法识别截图", report.Errors[0], System.Windows.MessageBoxImage.Error);
        }
        else
        {
            ShowMessage(
                "选区内未发现二维码",
                $"请重新框选完整二维码，或输入 {GetActionKeyword()} clipboard 识别剪贴板图片。",
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void ShowMessage(string title, string message, System.Windows.MessageBoxImage icon)
    {
        if (disposed)
        {
            return;
        }

        void Show() => System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            icon);
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            var thread = new Thread(Show) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        else if (dispatcher.CheckAccess())
        {
            Show();
        }
        else
        {
            dispatcher.BeginInvoke(Show);
        }
    }

    private void RefreshQuery(string command)
    {
        context?.API.ChangeQuery($"{GetActionKeyword()} {command}", true);
    }

    private string GetActionKeyword()
    {
        var keyword = context?.CurrentPluginMetadata.ActionKeyword;
        return string.IsNullOrWhiteSpace(keyword) ? "qr" : keyword;
    }

    private bool OpenPowerToysSettings()
    {
        var executablePath = FindPowerToysExecutable();
        if (executablePath is null)
        {
            ShowMessage(
                "无法打开设置",
                "请从 PowerToys 系统托盘菜单打开设置，然后进入 PowerToys Run 的插件列表。",
                System.Windows.MessageBoxImage.Warning);
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
            };
            startInfo.ArgumentList.Add("--open-settings=PowerLauncher");
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowMessage("无法打开设置", ex.Message, System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    private static string? FindPowerToysExecutable()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("PowerToys"))
            {
                using (process)
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Fall through to paths relative to the PowerToys Run host.
        }

        var hostPath = Environment.ProcessPath;
        var hostDirectory = string.IsNullOrWhiteSpace(hostPath) ? null : Path.GetDirectoryName(hostPath);
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "PowerToys.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "PowerToys.exe"),
            hostDirectory is null ? null : Path.Combine(hostDirectory, "PowerToys.exe"),
            hostDirectory is null ? null : Path.Combine(hostDirectory, "..", "PowerToys.exe"),
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .FirstOrDefault(File.Exists);
    }

    private static bool MatchesCommand(string search, params string[] commands) =>
        commands.Any(command => string.Equals(search, command, StringComparison.OrdinalIgnoreCase));

    private static bool HasCommandPrefix(string search, params string[] commands) =>
        commands.Any(command =>
            search.Equals(command, StringComparison.OrdinalIgnoreCase) ||
            search.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase));

    private static string RemoveCommandPrefix(string search, params string[] commands)
    {
        foreach (var command in commands)
        {
            if (search.Equals(command, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (search.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase))
            {
                return search[(command.Length + 1)..].Trim();
            }
        }

        return search;
    }

    private static int ReadNumberOption(
        IEnumerable<PluginAdditionalOption>? options,
        string key,
        int defaultValue,
        int min,
        int max)
    {
        var value = options?.FirstOrDefault(option => option.Key == key)?.NumberValue ?? defaultValue;
        return Math.Clamp((int)Math.Round(value), min, max);
    }

    private void UpdateIconPath(Theme theme)
    {
        iconPath = theme is Theme.Light or Theme.HighContrastWhite
            ? "Images/localqrscanner.light.png"
            : "Images/localqrscanner.dark.png";
    }

    private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);
}
