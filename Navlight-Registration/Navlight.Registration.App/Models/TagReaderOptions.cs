using System.Text.Json;
using System.Text.Json.Nodes;

namespace Navlight.Registration.App.Models;

public sealed class TagReaderOptions
{
    public string PortName { get; init; } = string.Empty;
    public int ResponseTimeoutMilliseconds { get; init; } = 1000;
    public int TagDetectTimeoutMilliseconds { get; init; } = -1;
    public bool ResetInterface { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PortName);

    public TimeSpan ResponseTimeout => TimeSpan.FromMilliseconds(Math.Max(1, ResponseTimeoutMilliseconds));

    public TimeSpan TagDetectTimeout => TagDetectTimeoutMilliseconds < 0
        ? Timeout.InfiniteTimeSpan
        : TimeSpan.FromMilliseconds(TagDetectTimeoutMilliseconds);

    public TagReaderOptions WithPortName(string portName)
    {
        return new TagReaderOptions
        {
            PortName = portName,
            ResponseTimeoutMilliseconds = ResponseTimeoutMilliseconds,
            TagDetectTimeoutMilliseconds = TagDetectTimeoutMilliseconds,
            ResetInterface = ResetInterface
        };
    }

    public static TagReaderOptions Load()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException($"Configuration file not found: {configPath}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!document.RootElement.TryGetProperty("TagReader", out var section))
        {
            return new TagReaderOptions();
        }

        return new TagReaderOptions
        {
            PortName = section.TryGetProperty("PortName", out var portName) ? portName.GetString() ?? string.Empty : string.Empty,
            ResponseTimeoutMilliseconds = section.TryGetProperty("ResponseTimeoutMilliseconds", out var responseTimeout)
                ? responseTimeout.GetInt32()
                : 1000,
            TagDetectTimeoutMilliseconds = section.TryGetProperty("TagDetectTimeoutMilliseconds", out var tagDetectTimeout)
                ? tagDetectTimeout.GetInt32()
                : -1,
            ResetInterface = section.TryGetProperty("ResetInterface", out var resetInterface) && resetInterface.GetBoolean()
        };
    }

    public void Save()
    {
        var configPath = GetConfigPath();
        JsonObject root;

        if (File.Exists(configPath))
        {
            root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
                ?? throw new InvalidOperationException($"Configuration file is not a JSON object: {configPath}");
        }
        else
        {
            root = [];
        }

        var section = root["TagReader"] as JsonObject ?? [];
        section["PortName"] = PortName;
        section["ResponseTimeoutMilliseconds"] = ResponseTimeoutMilliseconds;
        section["TagDetectTimeoutMilliseconds"] = TagDetectTimeoutMilliseconds;
        section["ResetInterface"] = ResetInterface;
        root["TagReader"] = section;

        File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static string GetConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }
}