using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UI;

public sealed class ScanResultWindow : Window
{
    private static readonly Brush MutedForeground = new SolidColorBrush(Color.FromRgb(95, 99, 104));
    private static ScanResultWindow? currentWindow;
    private readonly IReadOnlyList<QrScanRecord> records;
    private readonly Action openHistory;

    private ScanResultWindow(IReadOnlyList<QrScanRecord> records, Action openHistory)
    {
        this.records = records;
        this.openHistory = openHistory;
        Title = "Local QR Scanner - 本次识别结果";
        Width = 760;
        MinWidth = 580;
        Height = 680;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent();
        Closed += (_, _) => currentWindow = null;
    }

    public static void ShowResults(IReadOnlyList<QrScanRecord> records, Action openHistory)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(openHistory);
        if (records.Count == 0)
        {
            return;
        }

        void ShowWindow()
        {
            currentWindow?.Close();
            currentWindow = new ScanResultWindow(records, openHistory);
            currentWindow.Show();
            currentWindow.Activate();
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ShowWindow();
        }
        else
        {
            dispatcher.Invoke(ShowWindow);
        }
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel { Margin = new Thickness(24) };
        var footer = BuildFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock
        {
            Text = records.Count == 1 ? "二维码识别完成" : $"二维码识别完成，共发现 {records.Count} 个",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
        });
        header.Children.Add(new TextBlock
        {
            Text = "结果已自动保存到历史记录，可直接在下方复制或预览。",
            Foreground = MutedForeground,
            Margin = new Thickness(0, 5, 0, 0),
        });
        root.Children.Add(header);

        var resultsPanel = new StackPanel();
        for (var index = 0; index < records.Count; index++)
        {
            resultsPanel.Children.Add(BuildResultCard(records[index], index));
        }

        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = resultsPanel,
        });
        return root;
    }

    private Border BuildResultCard(QrScanRecord record, int index)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = records.Count == 1
                ? ContentClassifier.GetDisplayName(record.ContentKind)
                : $"结果 {index + 1} · {ContentClassifier.GetDisplayName(record.ContentKind)}",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });

        var capture = LoadImageFile(record.CapturePath);
        if (capture is not null)
        {
            panel.Children.Add(new System.Windows.Controls.Image
            {
                Source = capture,
                MaxHeight = 210,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10),
            });
        }

        panel.Children.Add(new TextBox
        {
            Text = record.Content,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 82,
            MaxHeight = 180,
            Padding = new Thickness(9),
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        buttons.Children.Add(CreateButton("复制内容", (_, _) => System.Windows.Clipboard.SetText(record.Content)));
        buttons.Children.Add(CreateButton("完整预览", (_, _) => PreviewWindow.ShowRecord(record)));
        panel.Children.Add(buttons);

        return new Border
        {
            Child = panel,
            BorderBrush = new SolidColorBrush(Color.FromRgb(216, 216, 216)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 14),
        };
    }

    private StackPanel BuildFooter()
    {
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        footer.Children.Add(CreateButton("打开全部历史", (_, _) =>
        {
            Close();
            openHistory();
        }));
        footer.Children.Add(CreateButton("关闭", (_, _) => Close(), isDefault: true));
        return footer;
    }

    private static System.Windows.Controls.Button CreateButton(string text, RoutedEventHandler handler, bool isDefault = false)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = text,
            MinWidth = 92,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
        };
        button.Click += handler;
        return button;
    }

    private static ImageSource? LoadImageFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
