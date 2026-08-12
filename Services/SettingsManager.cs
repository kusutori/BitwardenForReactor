using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using BitwardenForReactor.Serialization;

namespace BitwardenForReactor.Services;

public sealed class SettingsManager
{
    public static SettingsManager Instance { get; } = new();

    private readonly string _settingsPath;

    public AppSettings Current { get; private set; } = new();

    private SettingsManager()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BitwardenForReactor");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateDefaultSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return Normalize(JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings());
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Current = Normalize(settings);
        var json = JsonSerializer.Serialize(Current, AppJsonContext.Default.AppSettings);
        await File.WriteAllTextAsync(_settingsPath, json);
    }

    public static string GetAccountsRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BitwardenForReactor", "accounts");

    private static AppSettings CreateDefaultSettings()
    {
        var id = Guid.Parse("9bb5ace8-0909-43a4-a893-02aceaf124ec");
        var account = new AccountSettings
        {
            Id = id,
            DisplayName = "默认账号",
            CliDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Bitwarden CLI"),
            AuthenticationMode = AccountAuthenticationMode.Password
        };
        return new AppSettings { ActiveAccountId = id, Accounts = [account] };
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        if (settings.Accounts.Count == 0)
        {
            var defaults = CreateDefaultSettings();
            return settings with { ActiveAccountId = defaults.ActiveAccountId, Accounts = defaults.Accounts };
        }

        var activeId = settings.Accounts.Any(account => account.Id == settings.ActiveAccountId)
            ? settings.ActiveAccountId
            : settings.Accounts[0].Id;
        return settings with { ActiveAccountId = activeId };
    }
}
