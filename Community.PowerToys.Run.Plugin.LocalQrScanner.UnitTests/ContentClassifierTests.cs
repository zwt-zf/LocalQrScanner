using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UnitTests;

[TestClass]
public class ContentClassifierTests
{
    [TestMethod]
    [DataRow("https://example.com/docs", QrContentKind.Link)]
    [DataRow("https://example.com/image.png", QrContentKind.RemoteImageLink)]
    [DataRow("WIFI:T:WPA;S:LocalNetwork;P:secret;;", QrContentKind.WifiConfiguration)]
    [DataRow("BEGIN:VCARD\nFN:Example\nEND:VCARD", QrContentKind.Contact)]
    [DataRow("hello from a QR code", QrContentKind.Text)]
    public void Classify_recognizes_common_content(string content, QrContentKind expected)
    {
        Assert.AreEqual(expected, ContentClassifier.Classify(content));
    }

    [TestMethod]
    public void Classify_recognizes_embedded_png()
    {
        var dataUri = "data:image/png;base64," + Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x00]);

        Assert.AreEqual(QrContentKind.EmbeddedImage, ContentClassifier.Classify(dataUri));
    }

    [TestMethod]
    public void Raw_png_bytes_can_be_wrapped_for_local_preview()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x00];

        var recognized = ContentClassifier.TryCreateImageDataUri(png, out var dataUri);

        Assert.IsTrue(recognized);
        StringAssert.StartsWith(dataUri, "data:image/png;base64,");
    }
}
