using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UnitTests;

[TestClass]
[DoNotParallelize]
public class MainTests
{
    [TestMethod]
    public void Empty_query_offers_capture_clipboard_history_and_settings_actions()
    {
        using var main = new Main();

        var results = main.Query(new Query(string.Empty));

        Assert.HasCount(4, results);
        StringAssert.Contains(results[0].Title, "截图");
        StringAssert.Contains(results[1].Title, "剪贴板");
        StringAssert.Contains(results[2].Title, "历史记录面板");
        StringAssert.Contains(results[3].Title, "设置");
    }

    [TestMethod]
    public void Scan_command_returns_region_capture_action()
    {
        using var main = new Main();

        var result = main.Query(new Query("scan")).Single();

        StringAssert.Contains(result.Title, "选区");
        Assert.IsNotNull(result.Action);
    }

    [TestMethod]
    public void History_command_returns_panel_action_instead_of_history_items()
    {
        using var main = new Main();

        var result = main.Query(new Query("history example")).Single();

        StringAssert.Contains(result.Title, "历史记录中搜索");
        Assert.IsNull(result.ContextData);
    }

    [TestMethod]
    public void Free_text_query_does_not_leak_history_items_into_run_results()
    {
        using var main = new Main();

        var results = main.Query(new Query("an old QR payload"));

        Assert.IsEmpty(results);
    }
}
