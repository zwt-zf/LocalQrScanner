using System.Drawing;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZXing;
using ZXing.Common;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UnitTests;

[TestClass]
public class ScreenQrScannerTests
{
    [TestMethod]
    public void ScanBitmap_decodes_generated_qr_code_offline()
    {
        const string payload = "Local QR Scanner works offline";
        var matrix = new MultiFormatWriter().encode(
            payload,
            BarcodeFormat.QR_CODE,
            320,
            320,
            new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 3 });
        using var bitmap = ToBitmap(matrix);

        var report = new ScreenQrScanner().ScanBitmap(bitmap, "unit test", createSnapshots: true);

        Assert.HasCount(1, report.Detections);
        Assert.AreEqual(payload, report.Detections[0].Content);
        Assert.IsNotNull(report.Detections[0].QrSnapshotPng);
    }

    [TestMethod]
    public void ScanBitmap_decodes_only_the_qr_code_inside_the_supplied_region()
    {
        using var leftQr = CreateQrBitmap("inside selected region");
        using var rightQr = CreateQrBitmap("outside selected region");
        using var desktop = new Bitmap(680, 320);
        using (var graphics = Graphics.FromImage(desktop))
        {
            graphics.Clear(Color.White);
            graphics.DrawImageUnscaled(leftQr, 0, 0);
            graphics.DrawImageUnscaled(rightQr, 360, 0);
        }

        using var selectedRegion = desktop.Clone(
            new Rectangle(0, 0, 320, 320),
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var report = new ScreenQrScanner().ScanBitmap(selectedRegion, "selected region", createSnapshots: false);

        Assert.HasCount(1, report.Detections);
        Assert.AreEqual("inside selected region", report.Detections[0].Content);
    }

    private static Bitmap CreateQrBitmap(string payload)
    {
        var matrix = new MultiFormatWriter().encode(
            payload,
            BarcodeFormat.QR_CODE,
            320,
            320,
            new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 3 });
        return ToBitmap(matrix);
    }

    private static Bitmap ToBitmap(BitMatrix matrix)
    {
        var bitmap = new Bitmap(matrix.Width, matrix.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        using var brush = new SolidBrush(Color.Black);
        for (var y = 0; y < matrix.Height; y++)
        {
            for (var x = 0; x < matrix.Width; x++)
            {
                if (matrix[x, y])
                {
                    graphics.FillRectangle(brush, x, y, 1, 1);
                }
            }
        }

        return bitmap;
    }
}
