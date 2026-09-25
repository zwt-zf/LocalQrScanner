namespace Community.PowerToys.Run.Plugin.LocalQrScanner.Models;

public sealed record QrScanRecord
{
    public required string Id { get; init; }

    public required string Content { get; init; }

    public required QrContentKind ContentKind { get; init; }

    public required DateTimeOffset ScannedAtUtc { get; init; }

    public required string BarcodeFormat { get; init; }

    public required string Source { get; init; }

    public string? CapturePath { get; init; }
}
