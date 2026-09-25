using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

public sealed class ScreenQrScanner
{
    private const int TileSize = 1400;
    private const int TileOverlap = 180;

    public ScanReport ScanBitmap(Bitmap bitmap, string source, bool createSnapshots)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        try
        {
            return new ScanReport(DecodeBitmap(bitmap, source, createSnapshots), 1, []);
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException or InvalidOperationException)
        {
            return new ScanReport([], 0, [ex.Message]);
        }
    }

    public static Bitmap? TryGetClipboardBitmap()
    {
        if (!System.Windows.Forms.Clipboard.ContainsImage())
        {
            return null;
        }

        using var image = System.Windows.Forms.Clipboard.GetImage();
        return image is null ? null : new Bitmap(image);
    }

    private static IReadOnlyList<QrDetection> DecodeBitmap(Bitmap bitmap, string source, bool createSnapshots)
    {
        var detections = new List<QrDetection>();
        DecodeView(bitmap, bitmap, 0, 0, source, createSnapshots, detections);

        if (detections.Count == 0 && (bitmap.Width > TileSize || bitmap.Height > TileSize))
        {
            var step = TileSize - TileOverlap;
            for (var y = 0; y < bitmap.Height; y += step)
            {
                for (var x = 0; x < bitmap.Width; x += step)
                {
                    var width = Math.Min(TileSize, bitmap.Width - x);
                    var height = Math.Min(TileSize, bitmap.Height - y);
                    if (x == 0 && y == 0 && width == bitmap.Width && height == bitmap.Height)
                    {
                        continue;
                    }

                    using var tile = bitmap.Clone(new Rectangle(x, y, width, height), PixelFormat.Format32bppArgb);
                    DecodeView(tile, bitmap, x, y, source, createSnapshots, detections);
                }
            }
        }

        return Deduplicate(detections);
    }

    private static void DecodeView(
        Bitmap view,
        Bitmap original,
        int offsetX,
        int offsetY,
        string source,
        bool createSnapshots,
        ICollection<QrDetection> output)
    {
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = [BarcodeFormat.QR_CODE],
            },
        };

        var pixels = GetBgraPixels(view);
        var results = reader.DecodeMultiple(
            pixels,
            view.Width,
            view.Height,
            RGBLuminanceSource.BitmapFormat.BGRA32) ?? [];
        foreach (var result in results)
        {
            var content = GetContent(result);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var snapshot = createSnapshots ? CreateSnapshot(original, result.ResultPoints, offsetX, offsetY) : null;
            output.Add(new QrDetection(content, result.BarcodeFormat.ToString(), source, snapshot));
        }
    }

    private static byte[] GetBgraPixels(Bitmap bitmap)
    {
        var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = checked(bitmap.Width * 4);
            var pixels = new byte[checked(rowBytes * bitmap.Height)];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowBytes, rowBytes);
            }

            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static string GetContent(Result result)
    {
        if (result.RawBytes is { Length: > 0 } && ContentClassifier.TryCreateImageDataUri(result.RawBytes, out var imageDataUri))
        {
            return imageDataUri;
        }

        if (!string.IsNullOrEmpty(result.Text))
        {
            return result.Text;
        }

        return result.RawBytes is { Length: > 0 }
            ? Convert.ToBase64String(result.RawBytes)
            : string.Empty;
    }

    private static byte[]? CreateSnapshot(Bitmap original, ResultPoint[]? points, int offsetX, int offsetY)
    {
        if (points is null || points.Length < 2)
        {
            return null;
        }

        var minX = points.Min(point => point.X) + offsetX;
        var maxX = points.Max(point => point.X) + offsetX;
        var minY = points.Min(point => point.Y) + offsetY;
        var maxY = points.Max(point => point.Y) + offsetY;
        var side = Math.Max(maxX - minX, maxY - minY);
        var padding = Math.Max(20f, side * 0.4f);
        var left = Math.Max(0, (int)Math.Floor(minX - padding));
        var top = Math.Max(0, (int)Math.Floor(minY - padding));
        var right = Math.Min(original.Width, (int)Math.Ceiling(maxX + padding));
        var bottom = Math.Min(original.Height, (int)Math.Ceiling(maxY + padding));

        if (right <= left || bottom <= top)
        {
            return null;
        }

        using var crop = original.Clone(new Rectangle(left, top, right - left, bottom - top), PixelFormat.Format32bppArgb);
        using var stream = new MemoryStream();
        crop.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static IReadOnlyList<QrDetection> Deduplicate(IEnumerable<QrDetection> detections)
    {
        return detections
            .GroupBy(item => (item.Content, item.BarcodeFormat), StringTupleComparer.Instance)
            .Select(group => group.First())
            .ToList();
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Content, string BarcodeFormat)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string Content, string BarcodeFormat) x, (string Content, string BarcodeFormat) y) =>
            string.Equals(x.Content, y.Content, StringComparison.Ordinal) &&
            string.Equals(x.BarcodeFormat, y.BarcodeFormat, StringComparison.Ordinal);

        public int GetHashCode((string Content, string BarcodeFormat) obj) => HashCode.Combine(obj.Content, obj.BarcodeFormat);
    }
}
