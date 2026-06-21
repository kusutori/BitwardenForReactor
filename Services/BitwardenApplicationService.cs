using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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
    private static readonly Guid LegacyProfileId = Guid.Parse("9bb5ace8-0909-43a4-a893-02aceaf124ec");

    public static BitwardenApplicationService Instance { get; } = new();

    private BitwardenCliClient _client;

    private BitwardenApplicationService()
    {
        _client = CreateClient(SettingsManager.Instance.Current);
    }

    public bool IsUnlocked => _client.Session.IsUnlocked;

    public void Reconfigure(AppSettings settings) => _client = CreateClient(settings);

    public async Task<AppStatus?> GetStatusAsync()
    {
        var result = await _client.GetStatusAsync();
        return result.IsSuccess && result.Value is not null ? Map(result.Value) : null;
    }

    public async Task<(bool Success, string Message)> UnlockAsync(string masterPassword)
    {
        var result = await _client.UnlockAsync(DelegateSecretProvider.FromMasterPassword(masterPassword));
        return result.IsSuccess
            ? (true, "密码库已解锁。")
            : (false, ToChineseError(result.Error?.Code, result.Error?.Message, "解锁失败。"));
    }

    public async Task<bool> LockAsync() => (await _client.LockAsync()).IsSuccess;

    public async Task<bool> SyncAsync() => (await _client.Synchronization.SyncAsync()).IsSuccess;

    public async Task<BitwardenItem[]?> GetItemsAsync()
    {
        var result = await _client.Vault.ListItemsAsync();
        return result.IsSuccess ? result.Value?.Select(Map).ToArray() : null;
    }

    public async Task<BitwardenItem[]?> GetTrashItemsAsync()
    {
        var result = await _client.Vault.ListItemsAsync(new VaultItemQuery { Trash = true });
        return result.IsSuccess ? result.Value?.Select(Map).ToArray() : null;
    }

    public async Task<BitwardenFolder[]?> GetFoldersAsync()
    {
        var result = await _client.Folders.ListAsync();
        return result.IsSuccess ? result.Value?.Select(folder => new BitwardenFolder { Object = folder.Object, Id = folder.Id, Name = folder.Name }).ToArray() : null;
    }

    public async Task<string?> GetTotpAsync(string itemId)
    {
        var result = await _client.Vault.GetTotpAsync(itemId);
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<bool> DeleteItemAsync(string itemId, bool permanent = false) =>
        (await _client.Vault.DeleteItemAsync(itemId, permanent)).IsSuccess;

    public async Task<bool> RestoreItemAsync(string itemId) =>
        (await _client.Vault.RestoreItemAsync(itemId)).IsSuccess;

    public async Task<bool> CreateItemAsync(JsonObject newItem) =>
        (await _client.Vault.CreateItemAsync(newItem)).IsSuccess;

    public async Task<bool> EditItemAsync(string itemId, JsonObject changedProperties) =>
        (await _client.Vault.EditItemAsync(itemId, changedProperties)).IsSuccess;

    public async Task<bool> CloneItemAsync(string itemId, string newName) =>
        (await _client.Vault.CloneItemAsync(itemId, newName)).IsSuccess;

    private static BitwardenCliClient CreateClient(AppSettings settings)
    {
        var defaultCliDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bitwarden CLI");
        var options = new BitwardenCliOptions
        {
            ExecutablePath = settings.BwPath,
            AdditionalEnvironment = settings.GetEnvironmentVariables()
        };
        return new BitwardenCliClientFactory(options).Create(new BitwardenAccountProfile
        {
            Id = LegacyProfileId,
            DisplayName = "默认账号",
            CliDataDirectory = defaultCliDirectory
        });
    }

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
