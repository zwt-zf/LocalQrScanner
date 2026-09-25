using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UI;

public sealed class PreviewWindow : Window
{
    private static readonly System.Windows.Media.Brush MutedForeground = new SolidColorBrush(Color.FromRgb(95, 99, 104));
    private readonly QrScanRecord record;

    public PreviewWindow(QrScanRecord record)
    {
        this.record = record;
        Title = "Local QR Scanner - 本地预览";
        Width = 680;
        MinWidth = 520;
        Height = 640;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent();
    }

    public static void ShowRecord(QrScanRecord record)
    {
        void ShowWindow()
        {
            var window = new PreviewWindow(record);
            window.Show();
            window.Activate();
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
        var buttons = BuildButtons();
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var content = new StackPanel();
        scroll.Content = content;
        root.Children.Add(scroll);

        content.Children.Add(new TextBlock
        {
            Text = ContentClassifier.GetDisplayName(record.ContentKind),
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        });
        content.Children.Add(new TextBlock
        {
            Text = $"{record.ScannedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}  ·  {record.Source}  ·  {record.BarcodeFormat}",
            Foreground = MutedForeground,
            Margin = new Thickness(0, 0, 0, 18),
        });

        var payloadImage = LoadPayloadImage();
        if (payloadImage is not null)
        {
            content.Children.Add(BuildImageSection("图片内容", payloadImage));
        }
        else if (record.ContentKind == QrContentKind.RemoteImageLink)
        {
            content.Children.Add(BuildInfoBox("这是远程图片链接。为保证纯本地运行，插件不会下载图片；可复制链接或由你明确选择在浏览器中打开。"));
        }
        else if (record.ContentKind == QrContentKind.LocalImage && !ContentClassifier.TryGetLocalImagePath(record.Content, out _))
        {
            content.Children.Add(BuildInfoBox("二维码指向的本地图片不存在或当前不可访问。"));
        }

        content.Children.Add(new TextBlock
        {
            Text = "二维码内容",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 6),
        });
        content.Children.Add(new TextBox
        {
            Text = record.Content,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 120,
            MaxHeight = 240,
            Padding = new Thickness(10),
        });

        var capture = LoadImageFile(record.CapturePath);
        if (capture is not null)
        {
            content.Children.Add(BuildImageSection("识别时的二维码截图", capture));
        }

        return root;
    }

    private StackPanel BuildButtons()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };

        panel.Children.Add(CreateButton("复制内容", (_, _) => System.Windows.Clipboard.SetText(record.Content)));

        if (GetOpenTarget() is not null)
        {
            panel.Children.Add(CreateButton(GetOpenButtonLabel(), (_, _) => OpenTarget()));
        }

        panel.Children.Add(CreateButton("关闭", (_, _) => Close(), isDefault: true));
        return panel;
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

    private static Border BuildImageSection(string title, ImageSource image)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(new System.Windows.Controls.Image
        {
            Source = image,
            MaxHeight = 260,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        return new Border
        {
            Child = panel,
            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 16),
        };
    }

    private static Border BuildInfoBox(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(242, 246, 252)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        };
    }

    private ImageSource? LoadPayloadImage()
    {
        if (ContentClassifier.TryGetEmbeddedImageBytes(record.Content, out var bytes))
        {
            return LoadImageBytes(bytes);
        }

        return ContentClassifier.TryGetLocalImagePath(record.Content, out var path)
            ? LoadImageFile(path)
            : null;
    }

    private static ImageSource? LoadImageFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return LoadImageBytes(File.ReadAllBytes(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static ImageSource? LoadImageBytes(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException)
        {
            return null;
        }
    }

    private string? GetOpenTarget()
    {
        if (ContentClassifier.TryGetSupportedUri(record.Content, out var uri))
        {
            return uri.AbsoluteUri;
        }

        return ContentClassifier.TryGetLocalImagePath(record.Content, out var path) ? path : null;
    }

    private string GetOpenButtonLabel()
    {
        return ContentClassifier.TryGetLocalImagePath(record.Content, out _) ? "打开本地文件" : "使用默认应用打开";
    }

    private void OpenTarget()
    {
        var target = GetOpenTarget();
        if (target is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "无法打开", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
