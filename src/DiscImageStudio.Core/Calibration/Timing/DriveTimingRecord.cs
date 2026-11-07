using System.Text.Json;
using System.Text.Json.Serialization;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Core.Calibration;

public static class DriveTimingRecord
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 16,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static DriveTimingResult Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException(L.T("Timing.InvalidRecord"));
        var result = JsonSerializer.Deserialize<DriveTimingResult>(stream, Options)
            ?? throw new InvalidDataException(L.T("Timing.InvalidRecord"));
        result.Validate();
        return result;
    }

    public static void Save(string path, DriveTimingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Validate();
        string target = Path.GetFullPath(path);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, result, Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
