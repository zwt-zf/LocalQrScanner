using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UI;

public sealed class HistoryWindow : Window
{
    private static readonly Brush MutedForeground = new SolidColorBrush(Color.FromRgb(95, 99, 104));
    private static HistoryWindow? currentWindow;

    private readonly HistoryStore historyStore;
    private readonly Func<ScannerSettings> getSettings;
    private readonly TextBox searchBox = new();
    private readonly ListBox historyList = new();
    private readonly StackPanel detailsPanel = new();
    private readonly TextBlock countLabel = new();

    private HistoryWindow(HistoryStore historyStore, Func<ScannerSettings> getSettings)
    {
        this.historyStore = historyStore;
        this.getSettings = getSettings;
        Title = "Local QR Scanner - 历史记录";
        Width = 980;
        MinWidth = 760;
        Height = 680;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent();
        Closed += (_, _) => currentWindow = null;
    }

    public static void ShowPanel(HistoryStore historyStore, Func<ScannerSettings> getSettings, string initialSearch = "")
    {
        void ShowWindow()
        {
            if (currentWindow is { IsLoaded: true })
            {
                currentWindow.SetSearch(initialSearch);
                currentWindow.RefreshHistory();
                currentWindow.Activate();
                return;
            }

            currentWindow = new HistoryWindow(historyStore, getSettings);
            currentWindow.SetSearch(initialSearch);
            currentWindow.RefreshHistory();
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
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        header.Children.Add(new TextBlock
        {
            Text = "二维码扫描历史",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        searchBox.Margin = new Thickness(16, 0, 0, 0);
        searchBox.Padding = new Thickness(9, 6, 9, 6);
        searchBox.ToolTip = "搜索内容、类型或来源";
        searchBox.TextChanged += (_, _) => RefreshHistory();
        Grid.SetColumn(searchBox, 1);
        header.Children.Add(searchBox);
        root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        historyList.BorderThickness = new Thickness(0);
        historyList.Padding = new Thickness(4);
        historyList.SelectionChanged += (_, _) => ShowSelectedRecord();
        historyList.MouseDoubleClick += (_, _) => PreviewSelectedRecord();
        body.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = historyList,
        });

        var detailsScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(22, 0, 0, 0),
            Content = detailsPanel,
        };
        Grid.SetColumn(detailsScroll, 1);
        body.Children.Add(detailsScroll);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        countLabel.Foreground = MutedForeground;
        countLabel.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(countLabel);
        var footerButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        footerButtons.Children.Add(CreateButton("刷新", (_, _) => RefreshHistory()));
        footerButtons.Children.Add(CreateButton("清空历史", (_, _) => ClearHistory()));
        footerButtons.Children.Add(CreateButton("关闭", (_, _) => Close(), isDefault: true));
        DockPanel.SetDock(footerButtons, Dock.Right);
        footer.Children.Add(footerButtons);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        ShowEmptyDetails("请选择一条记录查看详情。");
        return root;
    }

    private void SetSearch(string search)
    {
        searchBox.Text = search;
        searchBox.CaretIndex = searchBox.Text.Length;
    }

    private void RefreshHistory()
    {
        var selectedId = (historyList.SelectedItem as HistoryItem)?.Record.Id;
        var search = searchBox.Text.Trim();
        var records = historyStore.GetAll(getSettings());
        var filtered = string.IsNullOrWhiteSpace(search)
            ? records
            : records.Where(record =>
                record.Content.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                ContentClassifier.GetDisplayName(record.ContentKind).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                record.Source.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        var items = filtered.Select(record => new HistoryItem(record)).ToArray();
        historyList.ItemsSource = items;
        historyList.SelectedItem = items.FirstOrDefault(item => string.Equals(item.Record.Id, selectedId, StringComparison.Ordinal));

        if (historyList.SelectedItem is null && items.Length > 0)
        {
            historyList.SelectedIndex = 0;
        }
        else if (items.Length == 0)
        {
            ShowEmptyDetails(string.IsNullOrWhiteSpace(search) ? "还没有扫描记录。" : "没有匹配的扫描记录。");
        }

        countLabel.Text = string.IsNullOrWhiteSpace(search)
            ? $"共 {items.Length} 条记录"
            : $"找到 {items.Length} 条记录";
    }

    private void ShowSelectedRecord()
    {
        if (historyList.SelectedItem is not HistoryItem item)
        {
            return;
        }

        var record = item.Record;
        detailsPanel.Children.Clear();
        detailsPanel.Children.Add(new TextBlock
        {
            Text = ContentClassifier.GetSummary(record.Content),
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 7),
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Text = $"{ContentClassifier.GetDisplayName(record.ContentKind)}  ·  {record.ScannedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n{record.Source}  ·  {record.BarcodeFormat}",
            Foreground = MutedForeground,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18),
        });

        var capture = LoadImageFile(record.CapturePath);
        if (capture is not null)
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = "识别时的二维码截图",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 7),
            });
            detailsPanel.Children.Add(new System.Windows.Controls.Image
            {
                Source = capture,
                MaxHeight = 260,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 18),
            });
        }

        detailsPanel.Children.Add(new TextBlock
        {
            Text = "二维码内容",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 7),
        });
        detailsPanel.Children.Add(new TextBox
        {
            Text = record.Content,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 120,
            MaxHeight = 260,
            Padding = new Thickness(10),
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        buttons.Children.Add(CreateButton("复制内容", (_, _) => System.Windows.Clipboard.SetText(record.Content)));
        buttons.Children.Add(CreateButton("完整预览", (_, _) => PreviewWindow.ShowRecord(record)));
        buttons.Children.Add(CreateButton("删除", (_, _) => DeleteRecord(record)));
        detailsPanel.Children.Add(buttons);
    }

    private void ShowEmptyDetails(string message)
    {
        detailsPanel.Children.Clear();
        detailsPanel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = MutedForeground,
            FontSize = 16,
            Margin = new Thickness(0, 10, 0, 0),
        });
    }

    private void PreviewSelectedRecord()
    {
        if (historyList.SelectedItem is HistoryItem item)
        {
            PreviewWindow.ShowRecord(item.Record);
        }
    }

    private void DeleteRecord(QrScanRecord record)
    {
        if (MessageBox.Show(this, "确定要删除这条扫描记录吗？", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        historyStore.Delete(record.Id);
        RefreshHistory();
    }

    private void ClearHistory()
    {
        if (MessageBox.Show(this, "确定要清空全部二维码扫描历史吗？", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        historyStore.Clear();
        RefreshHistory();
    }

    private static System.Windows.Controls.Button CreateButton(string text, RoutedEventHandler handler, bool isDefault = false)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = text,
            MinWidth = 88,
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

    private sealed record HistoryItem(QrScanRecord Record)
    {
        public override string ToString() =>
            $"{ContentClassifier.GetSummary(Record.Content, 44)}\n{ContentClassifier.GetDisplayName(Record.ContentKind)} · {Record.ScannedAtUtc.ToLocalTime():MM-dd HH:mm}";
    }
}
