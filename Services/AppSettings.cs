using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BitwardenForReactor.Services;

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

public sealed record AppSettings
{
    public AppThemeMode ThemeMode { get; init; } = AppThemeMode.System;

    [JsonPropertyName("isDarkMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyIsDarkMode { get; init; }

    public string BwPath { get; init; } = "bw";

    public string CustomEnvironment { get; init; } = string.Empty;

    public string BwClientId { get; init; } = string.Empty;

    public string BwClientSecret { get; init; } = string.Empty;

    public int ClipboardClearSeconds { get; init; } = 30;

    public int AutoLockMinutes { get; init; } = 15;

    public Dictionary<string, string> GetEnvironmentVariables()
    {
        var result = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(BwClientId))
        {
            result["BW_CLIENTID"] = BwClientId;
        }

        if (!string.IsNullOrWhiteSpace(BwClientSecret))
        {
            result["BW_CLIENTSECRET"] = BwClientSecret;
        }

        if (string.IsNullOrWhiteSpace(CustomEnvironment))
        {
            return result;
        }

        foreach (var pair in CustomEnvironment.Split(';', System.StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return result;
    }
}
