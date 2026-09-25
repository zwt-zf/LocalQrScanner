using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UnitTests;

[TestClass]
[DoNotParallelize]
public class HistoryStoreTests
{
    private string directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "LocalQrScanner.Tests", Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void AddRange_persists_content_and_snapshot()
    {
        var store = new HistoryStore(directory);
        var settings = new ScannerSettings(30, 200, true);

        var added = store.AddRange([new QrDetection("https://example.com", "QR_CODE", "test", [1, 2, 3])], settings);
        var reloaded = new HistoryStore(directory).GetAll(settings);

        Assert.HasCount(1, added);
        Assert.HasCount(1, reloaded);
        Assert.AreEqual("https://example.com", reloaded[0].Content);
        Assert.AreEqual(QrContentKind.Link, reloaded[0].ContentKind);
        Assert.IsTrue(File.Exists(reloaded[0].CapturePath));
    }

    [TestMethod]
    public void Max_history_setting_removes_oldest_record()
    {
        var store = new HistoryStore(directory);
        var settings = new ScannerSettings(30, 1, false);

        store.AddRange(
        [
            new QrDetection("first", "QR_CODE", "test", null),
            new QrDetection("second", "QR_CODE", "test", null),
        ], settings);

        var records = store.GetAll(settings);
        Assert.HasCount(1, records);
        Assert.AreEqual("second", records[0].Content);
    }
}
