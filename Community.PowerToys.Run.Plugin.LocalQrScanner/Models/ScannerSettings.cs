namespace Community.PowerToys.Run.Plugin.LocalQrScanner.Models;

public sealed record ScannerSettings(int RetentionDays, int MaxHistoryItems, bool SaveQrSnapshots)
{
    public static ScannerSettings Default { get; } = new(30, 200, true);
}
