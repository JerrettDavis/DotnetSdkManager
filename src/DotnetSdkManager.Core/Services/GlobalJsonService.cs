using System.Text.Json;
using System.Text.Json.Nodes;
using DotnetSdkManager.IO;
using DotnetSdkManager.Models;

namespace DotnetSdkManager.Services;

public sealed class GlobalJsonService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<string> WriteAsync(
        string directory,
        string version,
        string? rollForward,
        bool? allowPrerelease,
        CancellationToken cancellationToken = default)
    {
        var parsedVersion = DotnetSdkVersion.Parse(version);
        var fullDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDirectory);
        var path = Path.Combine(fullDirectory, "global.json");

        JsonObject root;
        if (File.Exists(path))
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            root = JsonNode.Parse(content) as JsonObject
                   ?? throw new JsonException($"'{path}' must contain a JSON object.");
        }
        else
        {
            root = new JsonObject();
        }

        var sdk = root["sdk"] as JsonObject ?? new JsonObject();
        root["sdk"] = sdk;
        sdk["version"] = parsedVersion.Original;

        if (parsedVersion.Major < 3)
        {
            sdk.Remove("rollForward");
            sdk.Remove("allowPrerelease");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(rollForward))
            {
                sdk["rollForward"] = rollForward;
            }

            if (allowPrerelease is { } value)
            {
                sdk["allowPrerelease"] = value;
            }
        }

        await AtomicFile.WriteAllTextAsync(path, root.ToJsonString(JsonOptions) + Environment.NewLine, cancellationToken);
        return path;
    }
}
