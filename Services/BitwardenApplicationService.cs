using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Threading;
using BitwardenCli.Core;
using BitwardenCli.Core.Accounts;
using BitwardenCli.Core.Authentication;
using BitwardenCli.Core.Models;
using BitwardenCli.Core.Results;
using BitwardenForReactor.Models;

namespace BitwardenForReactor.Services;

/// <summary>Adapts the reusable CLI package to the application's current UI models.</summary>
public sealed class BitwardenApplicationService
{
    public static BitwardenApplicationService Instance { get; } = new();

    private BitwardenCliClientFactory? _factory;
    private readonly Dictionary<Guid, BitwardenCliClient> _clients = [];
    private Guid _activeAccountId;
    private string _optionsFingerprint = string.Empty;

    private BitwardenApplicationService()
    {
        Reconfigure(SettingsManager.Instance.Current);
    }

    private BitwardenCliClient ActiveClient => _clients[_activeAccountId];

    public bool IsUnlocked => ActiveClient.Session.IsUnlocked;

    public void Reconfigure(AppSettings settings)
    {
        var environment = settings.Cli.GetEnvironmentVariables();
        var fingerprint = settings.Cli.ExecutablePath + "\n" + string.Join("\n", environment.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
        if (_factory is null || !string.Equals(_optionsFingerprint, fingerprint, StringComparison.Ordinal))
        {
            _factory = new BitwardenCliClientFactory(new BitwardenCliOptions
            {
                ExecutablePath = settings.Cli.ExecutablePath,
                AdditionalEnvironment = environment
            });
            _clients.Clear();
            _optionsFingerprint = fingerprint;
        }

        foreach (var account in settings.Accounts)
        {
            if (!_clients.ContainsKey(account.Id))
            {
                _clients.Add(account.Id, _factory.Create(Map(account)));
            }
        }

        foreach (var removedId in _clients.Keys.Except(settings.Accounts.Select(account => account.Id)).ToArray())
        {
            _factory.Remove(removedId);
            _clients.Remove(removedId);
        }

        _activeAccountId = settings.ActiveAccountId;
    }

    public bool SwitchAccount(Guid accountId)
    {
        if (!_clients.ContainsKey(accountId)) return false;
        _activeAccountId = accountId;
        return true;
    }

    public async Task<BitwardenStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await ActiveClient.GetStatusAsync(cancellationToken);
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<(bool Success, string Message)> UnlockAsync(string masterPassword)
    {
        var result = await ActiveClient.UnlockAsync(DelegateSecretProvider.FromMasterPassword(masterPassword));
        return result.IsSuccess
            ? (true, "密码库已解锁。")
            : (false, ToChineseError(result.Error?.Code, result.Error?.Message, "解锁失败。"));
    }

    public async Task<bool> LockAsync() => (await ActiveClient.LockAsync()).IsSuccess;

    public async Task<bool> SyncAsync() => (await ActiveClient.Synchronization.SyncAsync()).IsSuccess;

    public Task<CliResult<IReadOnlyList<BitwardenItem>>> GetItemsResultAsync(CancellationToken cancellationToken = default) =>
        ActiveClient.Vault.ListItemsAsync(cancellationToken: cancellationToken);

    public Task<CliResult<IReadOnlyList<BitwardenItem>>> GetTrashItemsResultAsync(CancellationToken cancellationToken = default) =>
        ActiveClient.Vault.ListItemsAsync(new VaultItemQuery { Trash = true }, cancellationToken);

    public Task<CliResult<IReadOnlyList<BitwardenFolder>>> GetFoldersResultAsync(CancellationToken cancellationToken = default) =>
        ActiveClient.Folders.ListAsync(cancellationToken: cancellationToken);

    public async Task<BitwardenFolder?> CreateFolderAsync(string name)
    {
        var result = await ActiveClient.Folders.CreateAsync(name);
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<BitwardenFolder?> EditFolderAsync(string id, string name)
    {
        var result = await ActiveClient.Folders.EditAsync(id, name);
        return result.IsSuccess ? result.Value : null;
    }

    public static string DescribeError(CliError? error, string fallback) =>
        ToChineseError(error?.Code, error?.Message, fallback);

    public async Task<string?> GetTotpAsync(string itemId)
    {
        var result = await ActiveClient.Vault.GetTotpAsync(itemId);
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<bool> DeleteItemAsync(string itemId, bool permanent = false) =>
        (await ActiveClient.Vault.DeleteItemAsync(itemId, permanent)).IsSuccess;

    public async Task<bool> RestoreItemAsync(string itemId) =>
        (await ActiveClient.Vault.RestoreItemAsync(itemId)).IsSuccess;

    public async Task<bool> CreateItemAsync(JsonObject newItem) =>
        (await ActiveClient.Vault.CreateItemAsync(newItem)).IsSuccess;

    public async Task<bool> EditItemAsync(string itemId, JsonObject changedProperties) =>
        (await ActiveClient.Vault.EditItemAsync(itemId, changedProperties)).IsSuccess;

    public async Task<bool> CloneItemAsync(string itemId, string newName) =>
        (await ActiveClient.Vault.CloneItemAsync(itemId, newName)).IsSuccess;

    public async Task<(bool Success, string Message)> LoginWithPasswordAsync(string email, string masterPassword)
    {
        var configured = await ConfigureServerIfNeededAsync();
        if (!configured.Success) return configured;
        var result = await ActiveClient.LoginAsync(
            new PasswordLoginRequest(email),
            DelegateSecretProvider.FromMasterPassword(masterPassword));
        return result.IsSuccess ? (true, "账号登录成功。") : (false, ToChineseError(result.Error?.Code, result.Error?.Message, "登录失败。"));
    }

    public async Task<(bool Success, string Message)> LoginWithApiKeyAsync(string clientId, string clientSecret)
    {
        var configured = await ConfigureServerIfNeededAsync();
        if (!configured.Success) return configured;
        var provider = new DelegateSecretProvider((_, purpose, _) => ValueTask.FromResult<string?>(
            purpose == SecretPurpose.ApiClientSecret ? clientSecret : null));
        var result = await ActiveClient.LoginAsync(new ApiKeyLoginRequest(clientId), provider);
        return result.IsSuccess ? (true, "API Key 登录成功。") : (false, ToChineseError(result.Error?.Code, result.Error?.Message, "登录失败。"));
    }

    public async Task<bool> LogoutAsync() => (await ActiveClient.LogoutAsync()).IsSuccess;

    public async Task<(bool Success, string Message)> LoginWithSsoAsync()
    {
        var configured = await ConfigureServerIfNeededAsync();
        if (!configured.Success) return configured;
        var result = await ActiveClient.LoginAsync(new SsoLoginRequest());
        return result.IsSuccess ? (true, "SSO 登录成功。") : (false, ToChineseError(result.Error?.Code, result.Error?.Message, "SSO 登录失败。"));
    }

    private async Task<(bool Success, string Message)> ConfigureServerIfNeededAsync()
    {
        if (string.IsNullOrWhiteSpace(ActiveClient.Profile.ServerUrl)) return (true, string.Empty);
        var result = await ActiveClient.Authentication.ConfigureServerAsync(ActiveClient.Profile.ServerUrl);
        return result.IsSuccess ? (true, string.Empty) : (false, ToChineseError(result.Error?.Code, result.Error?.Message, "服务器配置失败。"));
    }

    private static BitwardenAccountProfile Map(AccountSettings account) => new()
    {
        Id = account.Id,
        DisplayName = account.DisplayName,
        CliDataDirectory = account.CliDataDirectory,
        Email = account.Email,
        UserId = account.UserId,
        ServerUrl = account.ServerUrl,
        AuthenticationKind = account.AuthenticationMode switch
        {
            AccountAuthenticationMode.ApiKey => BitwardenAuthenticationKind.ApiKey,
            AccountAuthenticationMode.Sso => BitwardenAuthenticationKind.Sso,
            _ => BitwardenAuthenticationKind.Password
        },
        LastUsedAt = account.LastUsedAt
    };

    private static string ToChineseError(BitwardenCli.Core.Results.CliErrorCode? code, string? message, string fallback) => code switch
    {
        BitwardenCli.Core.Results.CliErrorCode.InvalidMasterPassword => "主密码不正确。",
        BitwardenCli.Core.Results.CliErrorCode.Unauthenticated => "账号尚未登录。",
        BitwardenCli.Core.Results.CliErrorCode.NetworkUnavailable => "网络不可用。",
        BitwardenCli.Core.Results.CliErrorCode.Timeout => "Bitwarden CLI 操作超时。",
        _ => string.IsNullOrWhiteSpace(message) ? fallback : message
    };
}
