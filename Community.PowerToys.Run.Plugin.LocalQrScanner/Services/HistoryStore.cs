using System.Text.Json;
using System.Text.Json.Serialization;
using Community.PowerToys.Run.Plugin.LocalQrScanner.Models;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.Services;

public sealed class HistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object sync = new();
    private readonly string historyFilePath;
    private readonly string capturesDirectory;
    private List<QrScanRecord>? records;

    public HistoryStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalQrScanner");
        historyFilePath = Path.Combine(DataDirectory, "history.json");
        capturesDirectory = Path.Combine(DataDirectory, "captures");
    }

    public string DataDirectory { get; }

    public IReadOnlyList<QrScanRecord> GetAll(ScannerSettings settings)
    {
        lock (sync)
        {
            EnsureLoaded();
            if (CleanupCore(settings))
            {
                SaveCore();
            }

            return records!.ToArray();
        }
    }

    public IReadOnlyList<QrScanRecord> AddRange(IEnumerable<QrDetection> detections, ScannerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(detections);
        lock (sync)
        {
            EnsureLoaded();
            Directory.CreateDirectory(DataDirectory);
            var now = DateTimeOffset.UtcNow;
            var added = new List<QrScanRecord>();
            foreach (var detection in detections)
            {
                var id = Guid.NewGuid().ToString("N");
                string? capturePath = null;
                if (settings.SaveQrSnapshots && detection.QrSnapshotPng is { Length: > 0 })
                {
                    try
                    {
                        Directory.CreateDirectory(capturesDirectory);
                        capturePath = Path.Combine(capturesDirectory, $"{id}.png");
                        File.WriteAllBytes(capturePath, detection.QrSnapshotPng);
                    }
                    catch (IOException)
                    {
                        capturePath = null;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        capturePath = null;
                    }
                }

                var record = new QrScanRecord
                {
                    Id = id,
                    Content = detection.Content,
                    ContentKind = ContentClassifier.Classify(detection.Content),
                    ScannedAtUtc = now,
                    BarcodeFormat = detection.BarcodeFormat,
                    Source = detection.Source,
                    CapturePath = capturePath,
                };
                records!.Insert(0, record);
                added.Add(record);
            }

            CleanupCore(settings);
            SaveCore();
            return added;
        }
    }

    public bool Delete(string id)
    {
        lock (sync)
        {
            EnsureLoaded();
            var record = records!.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (record is null)
            {
                return false;
            }

            records!.Remove(record);
            DeleteCapture(record.CapturePath);
            SaveCore();
            return true;
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            EnsureLoaded();
            foreach (var record in records!)
            {
                DeleteCapture(record.CapturePath);
            }

            records.Clear();
            SaveCore();
        }
    }

    public void ApplyRetention(ScannerSettings settings)
    {
        lock (sync)
        {
            EnsureLoaded();
            if (CleanupCore(settings))
            {
                SaveCore();
            }
        }
    }

    private void EnsureLoaded()
    {
        if (records is not null)
        {
            return;
        }

        if (!File.Exists(historyFilePath))
        {
            records = [];
            return;
        }

        try
        {
            var json = File.ReadAllText(historyFilePath);
            records = JsonSerializer.Deserialize<List<QrScanRecord>>(json, JsonOptions) ?? [];
            records = records.OrderByDescending(item => item.ScannedAtUtc).ToList();
        }
        catch (JsonException)
        {
            Directory.CreateDirectory(DataDirectory);
            var backup = Path.Combine(DataDirectory, $"history.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            File.Move(historyFilePath, backup, true);
            records = [];
        }
    }

    private bool CleanupCore(ScannerSettings settings)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-settings.RetentionDays);
        var removed = records!
            .Where((item, index) => item.ScannedAtUtc < cutoff || index >= settings.MaxHistoryItems)
            .ToList();
        if (removed.Count == 0)
        {
            return false;
        }

        foreach (var record in removed)
        {
            records!.Remove(record);
            DeleteCapture(record.CapturePath);
        }

        return true;
    }

    private void SaveCore()
    {
        Directory.CreateDirectory(DataDirectory);
        var temporaryPath = string.Concat(historyFilePath, ".tmp");
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(records, JsonOptions));
        File.Move(temporaryPath, historyFilePath, true);
    }

    private static void DeleteCapture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A preview window may still be using the file. The stale file is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // History removal must still succeed if an image file is locked.
        }
    }
}
