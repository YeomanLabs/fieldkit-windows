using System.IO.Compression;
using System.Text;
using FieldKit.Core.Domain;
using FieldKit.Core.Services;

namespace FieldKit.Core.DeviceLogs;

/// <summary>
/// Reads Intune Management Extension logs from files the engineer picks: a Collect diagnostics zip
/// or the log files themselves. Read-only; nothing is kept.
/// </summary>
public sealed class DeviceLogReader : IDeviceLogReader
{
    /// <summary>Bigger than any log the extension writes (it rolls over at about 3 MB).</summary>
    private const long MaxFileBytes = 32 * 1024 * 1024;

    public async Task<DeviceLogFiles> FromFilesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var source = string.Join(", ", paths.Select(Path.GetFileName));
        var files = new List<DeviceLogFile>();
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            try
            {
                files.AddRange(await Task.Run(() => Read(path), cancellationToken));
            }
            catch (InvalidDataException)
            {
                throw new DeviceLogException($"{name} isn't a zip file FieldKit can open.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new DeviceLogException($"Couldn't open {name}. Check it isn't open in another app, then try again.");
            }
        }
        return files.Count > 0
            ? new DeviceLogFiles(source, files)
            : throw new DeviceLogException($"{source} has no Intune Management Extension logs. Open the zip Intune's Collect diagnostics made for this device, or AppWorkload.log from the device.");
    }

    private static List<DeviceLogFile> Read(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            if (new FileInfo(path).Length > MaxFileBytes) return [];
            // A log copied straight off a device may still be open somewhere, so share everything.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return [new DeviceLogFile(Path.GetFileName(path), Text(stream))];
        }
        // Diagnostics zips put the logs in a folder named after what was collected; only the file names matter.
        using var zip = ZipFile.OpenRead(path);
        var entries = zip.Entries.Where(entry => entry.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var files = new List<DeviceLogFile>();
        foreach (var name in DeviceLogFiles.Pick(entries.Keys))
        {
            var entry = entries[name];
            if (entry.Length > MaxFileBytes) continue;
            using var stream = entry.Open();
            files.Add(new DeviceLogFile(name, Text(stream)));
        }
        return files;
    }

    private static string Text(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
