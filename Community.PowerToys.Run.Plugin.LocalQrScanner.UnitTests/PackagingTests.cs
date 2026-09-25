using System.Text.Json;
using System.Reflection;
using System.Runtime.Loader;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UnitTests;

[TestClass]
public class PackagingTests
{
    [TestMethod]
    public void Manifest_uses_host_context_and_ships_local_zxing_dependency()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "plugin.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;

        Assert.IsFalse(root.GetProperty("DynamicLoading").GetBoolean());
        Assert.AreEqual("0.1.1", root.GetProperty("Version").GetString());
        Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, "zxing.dll")));
    }

    [TestMethod]
    public void Local_dependency_resolver_loads_zxing_into_requested_context()
    {
        var dependencyPath = Path.Combine(AppContext.BaseDirectory, "zxing.dll");
        var requestedAssembly = AssemblyName.GetAssemblyName(dependencyPath);
        var loadContext = new AssemblyLoadContext("LocalQrScanner dependency test", isCollectible: true);

        var assembly = PluginDependencyResolver.LoadKnownDependency(loadContext, requestedAssembly, AppContext.BaseDirectory);

        Assert.IsNotNull(assembly);
        Assert.AreEqual(requestedAssembly.FullName, assembly.FullName);
        loadContext.Unload();
    }
}
