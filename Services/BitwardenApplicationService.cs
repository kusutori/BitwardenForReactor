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
using BitwardenForReactor.Models;
using CoreItem = BitwardenCli.Core.Models.VaultItem;
using CoreStatus = BitwardenCli.Core.Models.BitwardenStatus;
using AppStatus = BitwardenForReactor.Models.BitwardenStatus;

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
        var environment = settings.GetEnvironmentVariables();
        var fingerprint = settings.BwPath + "\n" + string.Join("\n", environment.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
        if (_factory is null || !string.Equals(_optionsFingerprint, fingerprint, StringComparison.Ordinal))
        {
            _factory = new BitwardenCliClientFactory(new BitwardenCliOptions
            {
                ExecutablePath = settings.BwPath,
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

    public async Task<AppStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await ActiveClient.GetStatusAsync(cancellationToken);
        return result.IsSuccess && result.Value is not null ? Map(result.Value) : null;
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

    public async Task<BitwardenItem[]?> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var result = await ActiveClient.Vault.ListItemsAsync(cancellationToken: cancellationToken);
        return result.IsSuccess ? result.Value?.Select(Map).ToArray() : null;
    }

    public async Task<BitwardenItem[]?> GetTrashItemsAsync(CancellationToken cancellationToken = default)
    {
        var result = await ActiveClient.Vault.ListItemsAsync(new VaultItemQuery { Trash = true }, cancellationToken);
        return result.IsSuccess ? result.Value?.Select(Map).ToArray() : null;
    }

    public async Task<BitwardenFolder[]?> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var result = await ActiveClient.Folders.ListAsync(cancellationToken: cancellationToken);
        return result.IsSuccess ? result.Value?.Select(folder => new BitwardenFolder { Object = folder.Object, Id = folder.Id, Name = folder.Name }).ToArray() : null;
    }

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

    private static AppStatus Map(CoreStatus status) => new()
    {
        ServerUrl = status.ServerUrl,
        LastSync = status.LastSync,
        UserEmail = status.UserEmail,
        UserId = status.UserId,
        Status = status.Status
    };

    private static BitwardenItem Map(CoreItem item) => new()
    {
        Id = item.Id,
        OrganizationId = item.OrganizationId,
        FolderId = item.FolderId,
        Type = (BitwardenItemType)(int)item.Type,
        Reprompt = item.Reprompt,
        Name = item.Name,
        Notes = item.Notes,
        Favorite = item.Favorite,
        CollectionIds = item.CollectionIds,
        RevisionDate = item.RevisionDate?.DateTime ?? default,
        CreationDate = item.CreationDate?.DateTime ?? default,
        DeletedDate = item.DeletedDate?.DateTime,
        Login = item.Login is null ? null : new LoginData
        {
            Username = item.Login.Username,
            Password = item.Login.Password,
            Totp = item.Login.Totp,
            PasswordRevisionDate = item.Login.PasswordRevisionDate?.DateTime,
            Uris = item.Login.Uris.Select(uri => new UriEntry { Match = uri.Match, Uri = uri.Uri }).ToArray()
        },
        SecureNote = item.SecureNote is null ? null : new SecureNoteData { Type = item.SecureNote.Type },
        Card = item.Card is null ? null : new CardData
        {
            CardholderName = item.Card.CardholderName, Brand = item.Card.Brand, Number = item.Card.Number,
            ExpMonth = item.Card.ExpMonth, ExpYear = item.Card.ExpYear, Code = item.Card.Code
        },
        Identity = item.Identity is null ? null : new IdentityData
        {
            Title = item.Identity.Title, FirstName = item.Identity.FirstName, MiddleName = item.Identity.MiddleName,
            LastName = item.Identity.LastName, Address1 = item.Identity.Address1, Address2 = item.Identity.Address2,
            Address3 = item.Identity.Address3, City = item.Identity.City, State = item.Identity.State,
            PostalCode = item.Identity.PostalCode, Country = item.Identity.Country, Company = item.Identity.Company,
            Email = item.Identity.Email, Phone = item.Identity.Phone, Ssn = item.Identity.Ssn,
            Username = item.Identity.Username, PassportNumber = item.Identity.PassportNumber,
            LicenseNumber = item.Identity.LicenseNumber
        },
        Fields = item.Fields.Select(field => new CustomField
        {
            Name = field.Name ?? string.Empty, Value = field.Value, Type = (CustomFieldType)(int)field.Type, LinkedId = field.LinkedId
        }).ToArray(),
        PasswordHistory = item.PasswordHistory.Select(entry => new PasswordHistoryEntry
        {
            LastUsedDate = entry.LastUsedDate?.DateTime ?? default, Password = entry.Password
        }).ToArray()
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
