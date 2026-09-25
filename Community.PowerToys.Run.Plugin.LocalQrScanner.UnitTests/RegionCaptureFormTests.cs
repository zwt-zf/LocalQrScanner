using System.Drawing;
using Community.PowerToys.Run.Plugin.LocalQrScanner.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UnitTests;

[TestClass]
public class RegionCaptureFormTests
{
    [TestMethod]
    public void NormalizeSelection_supports_dragging_up_and_left()
    {
        var selection = RegionCaptureForm.NormalizeSelection(new Point(300, 240), new Point(100, 80));

        Assert.AreEqual(new Rectangle(100, 80, 200, 160), selection);
    }
}
