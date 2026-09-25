using System.Text.RegularExpressions;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

public static partial class ContentClassifier
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp",
    };

    public static QrContentKind Classify(string content)
    {
        var value = content.Trim();
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetEmbeddedImageBytes(value, out _) ? QrContentKind.EmbeddedImage : QrContentKind.Data;
        }

        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return QrContentKind.Data;
        }

        if (value.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase))
        {
            return QrContentKind.WifiConfiguration;
        }

        if (value.StartsWith("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("MECARD:", StringComparison.OrdinalIgnoreCase))
        {
            return QrContentKind.Contact;
        }

        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return QrContentKind.Email;
        }

        if (TryGetSupportedUri(value, out var uri))
        {
            if (uri.IsFile && IsImagePath(uri.LocalPath))
            {
                return QrContentKind.LocalImage;
            }

            if ((uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && IsImagePath(uri.AbsolutePath))
            {
                return QrContentKind.RemoteImageLink;
            }

            return QrContentKind.Link;
        }

        if (Path.IsPathRooted(value) && IsImagePath(value))
        {
            return QrContentKind.LocalImage;
        }

        if (TryDecodeBase64Image(value, out _))
        {
            return QrContentKind.EmbeddedImage;
        }

        return LooksLikeData().IsMatch(value) ? QrContentKind.Data : QrContentKind.Text;
    }

    public static bool TryGetSupportedUri(string content, out Uri uri)
    {
        if (Uri.TryCreate(content.Trim(), UriKind.Absolute, out var candidate) &&
            candidate.Scheme is "http" or "https" or "file" or "mailto" or "tel")
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }

    public static bool TryGetEmbeddedImageBytes(string content, out byte[] bytes)
    {
        var value = content.Trim();
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var comma = value.IndexOf(',');
            if (comma > 0 && value[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bytes = Convert.FromBase64String(value[(comma + 1)..]);
                    return IsKnownImage(bytes);
                }
                catch (FormatException)
                {
                    // The payload is not valid base64.
                }
            }
        }
        else if (TryDecodeBase64Image(value, out bytes))
        {
            return true;
        }

        bytes = [];
        return false;
    }

    public static bool TryCreateImageDataUri(byte[] bytes, out string dataUri)
    {
        var mediaType = GetImageMediaType(bytes);
        if (mediaType is null)
        {
            dataUri = string.Empty;
            return false;
        }

        dataUri = $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        return true;
    }

    public static bool TryGetLocalImagePath(string content, out string path)
    {
        var value = content.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            path = uri.LocalPath;
        }
        else
        {
            path = value;
        }

        return Path.IsPathRooted(path) && IsImagePath(path) && File.Exists(path);
    }

    public static string GetDisplayName(QrContentKind kind) => kind switch
    {
        QrContentKind.Link => "链接",
        QrContentKind.RemoteImageLink => "远程图片链接",
        QrContentKind.EmbeddedImage => "内嵌图片",
        QrContentKind.LocalImage => "本地图片",
        QrContentKind.WifiConfiguration => "Wi-Fi 配置",
        QrContentKind.Contact => "联系人",
        QrContentKind.Email => "电子邮件",
        QrContentKind.Data => "结构化数据",
        _ => "文本",
    };

    public static string GetSummary(string content, int maxLength = 90)
    {
        var oneLine = Whitespace().Replace(content.Trim(), " ");
        if (oneLine.Length <= maxLength)
        {
            return oneLine;
        }

        return string.Concat(oneLine.AsSpan(0, maxLength - 1), "…");
    }

    private static bool IsImagePath(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    private static bool TryDecodeBase64Image(string value, out byte[] bytes)
    {
        bytes = [];
        if (value.Length < 16 || value.Length % 4 != 0 || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(value);
            return IsKnownImage(bytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsKnownImage(ReadOnlySpan<byte> bytes) => GetImageMediaType(bytes) is not null;

    private static string? GetImageMediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
        {
            return "image/png";
        }

        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return "image/jpeg";
        }

        if (bytes.StartsWith(new byte[] { 0x47, 0x49, 0x46, 0x38 }))
        {
            return "image/gif";
        }

        if (bytes.StartsWith(new byte[] { 0x42, 0x4D }))
        {
            return "image/bmp";
        }

        return bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)
            ? "image/webp"
            : null;
    }

    [GeneratedRegex(@"^(?:[A-Z][A-Z0-9_-]*:|\{.*\}|\[.*\])", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LooksLikeData();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
