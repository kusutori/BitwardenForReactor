using System.Collections.Generic;
using System;

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

    public string BwPath { get; init; } = "bw";

    public string CustomEnvironment { get; init; } = string.Empty;

    public int ClipboardClearSeconds { get; init; } = 30;

    public int AutoLockMinutes { get; init; } = 15;

    public Guid ActiveAccountId { get; init; }

    public IReadOnlyList<AccountSettings> Accounts { get; init; } = [];

    public Dictionary<string, string> GetEnvironmentVariables()
    {
        var result = new Dictionary<string, string>();

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

public enum AccountAuthenticationMode
{
    Password,
    ApiKey,
    Sso
}

public sealed record AccountSettings
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public required string CliDataDirectory { get; init; }
    public string? Email { get; init; }
    public string? UserId { get; init; }
    public string? ServerUrl { get; init; }
    public AccountAuthenticationMode AuthenticationMode { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
}
