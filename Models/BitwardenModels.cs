using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BitwardenForReactor.Models;

public sealed record BitwardenStatus
{
    [JsonPropertyName("serverUrl")]
    public string? ServerUrl { get; init; }

    [JsonPropertyName("lastSync")]
    public string? LastSync { get; init; }

    [JsonPropertyName("userEmail")]
    public string? UserEmail { get; init; }

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "unauthenticated";

    [JsonIgnore]
    public bool IsLoggedIn => Status != "unauthenticated";

    [JsonIgnore]
    public bool IsUnlocked => Status == "unlocked";

    [JsonIgnore]
    public bool IsLocked => Status == "locked";
}

public sealed record BitwardenFolder
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = "folder";

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed record BitwardenItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("organizationId")]
    public string? OrganizationId { get; init; }

    [JsonPropertyName("folderId")]
    public string? FolderId { get; init; }

    [JsonPropertyName("type")]
    public BitwardenItemType Type { get; init; }

    [JsonPropertyName("reprompt")]
    public int Reprompt { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("favorite")]
    public bool Favorite { get; init; }

    [JsonPropertyName("collectionIds")]
    public IReadOnlyList<string>? CollectionIds { get; init; }

    [JsonPropertyName("revisionDate")]
    public DateTime RevisionDate { get; init; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; init; }

    [JsonPropertyName("deletedDate")]
    public DateTime? DeletedDate { get; init; }

    [JsonPropertyName("login")]
    public LoginData? Login { get; init; }

    [JsonPropertyName("secureNote")]
    public SecureNoteData? SecureNote { get; init; }

    [JsonPropertyName("card")]
    public CardData? Card { get; init; }

    [JsonPropertyName("identity")]
    public IdentityData? Identity { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<CustomField>? Fields { get; init; }

    [JsonPropertyName("passwordHistory")]
    public IReadOnlyList<PasswordHistoryEntry>? PasswordHistory { get; init; }

    [JsonIgnore]
    public string? Username => Login?.Username ?? Identity?.Username;

    [JsonIgnore]
    public string? Password => Login?.Password;

    [JsonIgnore]
    public string? PrimaryUri => Login?.Uris?.FirstOrDefault()?.Uri;

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        BitwardenItemType.Login => "登录",
        BitwardenItemType.SecureNote => "笔记",
        BitwardenItemType.Card => "卡片",
        BitwardenItemType.Identity => "身份",
        _ => "项目"
    };
}

public sealed record LoginData
{
    [JsonPropertyName("uris")]
    public IReadOnlyList<UriEntry>? Uris { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("totp")]
    public string? Totp { get; init; }

    [JsonPropertyName("passwordRevisionDate")]
    public DateTime? PasswordRevisionDate { get; init; }
}

public sealed record UriEntry
{
    [JsonPropertyName("match")]
    public int? Match { get; init; }

    [JsonPropertyName("uri")]
    public string? Uri { get; init; }
}

public sealed record SecureNoteData
{
    [JsonPropertyName("type")]
    public int Type { get; init; }
}

public sealed record CardData
{
    [JsonPropertyName("cardholderName")]
    public string? CardholderName { get; init; }

    [JsonPropertyName("brand")]
    public string? Brand { get; init; }

    [JsonPropertyName("number")]
    public string? Number { get; init; }

    [JsonPropertyName("expMonth")]
    public string? ExpMonth { get; init; }

    [JsonPropertyName("expYear")]
    public string? ExpYear { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

public sealed record IdentityData
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("middleName")]
    public string? MiddleName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("address1")]
    public string? Address1 { get; init; }

    [JsonPropertyName("address2")]
    public string? Address2 { get; init; }

    [JsonPropertyName("address3")]
    public string? Address3 { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("company")]
    public string? Company { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("ssn")]
    public string? Ssn { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("passportNumber")]
    public string? PassportNumber { get; init; }

    [JsonPropertyName("licenseNumber")]
    public string? LicenseNumber { get; init; }
}

public sealed record CustomField
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("type")]
    public CustomFieldType Type { get; init; }

    [JsonPropertyName("linkedId")]
    public int? LinkedId { get; init; }
}

public sealed record PasswordHistoryEntry
{
    [JsonPropertyName("lastUsedDate")]
    public DateTime LastUsedDate { get; init; }

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}

public enum BitwardenItemType
{
    Login = 1,
    SecureNote = 2,
    Card = 3,
    Identity = 4
}

public enum CustomFieldType
{
    Text = 0,
    Hidden = 1,
    Boolean = 2,
    Linked = 3
}

public enum VaultFilter
{
    AllItems,
    Logins,
    Cards,
    Identities,
    Notes,
    Favorites,
    Trash
}

public sealed record VaultItemDraft(
    string? Id,
    BitwardenItemType Type,
    string Name,
    string? Username,
    string? Password,
    string? Uri,
    string? Notes,
    string? CardholderName,
    string? CardBrand,
    string? CardNumber,
    string? CardExpMonth,
    string? CardExpYear,
    string? CardCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Company,
    string? Address,
    bool Favorite)
{
    public static VaultItemDraft New(BitwardenItemType type = BitwardenItemType.Login) =>
        new(null, type, string.Empty, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, false);

    public static VaultItemDraft FromItem(BitwardenItem item)
    {
        var identity = item.Identity;
        var address = identity is null
            ? null
            : string.Join(", ", new[] { identity.Address1, identity.Address2, identity.Address3, identity.City, identity.State, identity.PostalCode, identity.Country }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        return new(
            item.Id,
            item.Type,
            item.Name,
            item.Login?.Username ?? item.Identity?.Username,
            item.Login?.Password,
            item.PrimaryUri,
            item.Notes,
            item.Card?.CardholderName,
            item.Card?.Brand,
            item.Card?.Number,
            item.Card?.ExpMonth,
            item.Card?.ExpYear,
            item.Card?.Code,
            identity?.FirstName,
            identity?.LastName,
            identity?.Email,
            identity?.Phone,
            identity?.Company,
            address,
            item.Favorite);
    }

    public JsonObject ToJsonObject()
    {
        var obj = new JsonObject
        {
            ["type"] = (int)Type,
            ["name"] = Name,
            ["notes"] = EmptyToNull(Notes),
            ["favorite"] = Favorite
        };

        switch (Type)
        {
            case BitwardenItemType.Login:
                obj["login"] = new JsonObject
                {
                    ["username"] = EmptyToNull(Username),
                    ["password"] = EmptyToNull(Password),
                    ["uris"] = string.IsNullOrWhiteSpace(Uri)
                        ? new JsonArray()
                        : new JsonArray(new JsonObject { ["uri"] = Uri })
                };
                break;
            case BitwardenItemType.Card:
                obj["card"] = new JsonObject
                {
                    ["cardholderName"] = EmptyToNull(CardholderName),
                    ["brand"] = EmptyToNull(CardBrand),
                    ["number"] = EmptyToNull(CardNumber),
                    ["expMonth"] = EmptyToNull(CardExpMonth),
                    ["expYear"] = EmptyToNull(CardExpYear),
                    ["code"] = EmptyToNull(CardCode)
                };
                break;
            case BitwardenItemType.Identity:
                obj["identity"] = new JsonObject
                {
                    ["firstName"] = EmptyToNull(FirstName),
                    ["lastName"] = EmptyToNull(LastName),
                    ["email"] = EmptyToNull(Email),
                    ["phone"] = EmptyToNull(Phone),
                    ["company"] = EmptyToNull(Company),
                    ["address1"] = EmptyToNull(Address)
                };
                break;
            case BitwardenItemType.SecureNote:
                obj["secureNote"] = new JsonObject { ["type"] = 0 };
                break;
        }

        return obj;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record IconInfo(string? Glyph, string? ImageUrl)
{
    public bool IsGlyph => !string.IsNullOrWhiteSpace(Glyph);

    public static IconInfo FromGlyph(string glyph) => new(glyph, null);

    public static IconInfo FromUrl(string url) => new(null, url);
}
