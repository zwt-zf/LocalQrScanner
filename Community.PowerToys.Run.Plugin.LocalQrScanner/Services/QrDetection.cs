namespace Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

public sealed record QrDetection(string Content, string BarcodeFormat, string Source, byte[]? QrSnapshotPng);

public sealed record ScanReport(IReadOnlyList<QrDetection> Detections, int ImagesScanned, IReadOnlyList<string> Errors);
