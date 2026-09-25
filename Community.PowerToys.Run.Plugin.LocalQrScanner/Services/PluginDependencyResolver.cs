using System.Reflection;
using System.Runtime.Loader;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

internal static class PluginDependencyResolver
{
    private const string ZxingAssemblyName = "zxing";
    private static int initialized;

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref initialized, 1) != 0)
        {
            return;
        }

        var pluginAssembly = typeof(PluginDependencyResolver).Assembly;
        var loadContext = AssemblyLoadContext.GetLoadContext(pluginAssembly) ?? AssemblyLoadContext.Default;
        loadContext.Resolving += ResolveDependency;
    }

    internal static Assembly? LoadKnownDependency(
        AssemblyLoadContext loadContext,
        AssemblyName requestedAssembly,
        string pluginDirectory)
    {
        ArgumentNullException.ThrowIfNull(loadContext);
        ArgumentNullException.ThrowIfNull(requestedAssembly);

        if (!string.Equals(requestedAssembly.Name, ZxingAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var assemblyPath = Path.Combine(pluginDirectory, "zxing.dll");
        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        var availableAssembly = AssemblyName.GetAssemblyName(assemblyPath);
        if (!IsCompatible(requestedAssembly, availableAssembly))
        {
            return null;
        }

        return loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
    }

    private static Assembly? ResolveDependency(AssemblyLoadContext loadContext, AssemblyName requestedAssembly)
    {
        var pluginDirectory = Path.GetDirectoryName(typeof(PluginDependencyResolver).Assembly.Location);
        return string.IsNullOrEmpty(pluginDirectory)
            ? null
            : LoadKnownDependency(loadContext, requestedAssembly, pluginDirectory);
    }

    private static bool IsCompatible(AssemblyName requested, AssemblyName available)
    {
        if (!string.Equals(requested.Name, available.Name, StringComparison.OrdinalIgnoreCase) ||
            requested.Version != available.Version)
        {
            return false;
        }

        var requestedToken = requested.GetPublicKeyToken();
        var availableToken = available.GetPublicKeyToken();
        return requestedToken is null || requestedToken.Length == 0 ||
               (availableToken is not null && requestedToken.SequenceEqual(availableToken));
    }
}
