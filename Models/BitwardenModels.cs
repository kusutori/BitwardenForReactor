using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace BitwardenForReactor.Models;

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
    string? FolderId,
    string? Username,
    string? Password,
    IReadOnlyList<VaultUriDraft> Uris,
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
    public bool IsFolder { get; init; }

    public static VaultItemDraft New(BitwardenItemType type = BitwardenItemType.Login) =>
        new(null, type, string.Empty, null, null, null, [VaultUriDraft.New()], null, null, null, null, null, null, null, null, null, null, null, null, null, false);

    public static VaultItemDraft NewFolder() => New() with { IsFolder = true };

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
            item.FolderId,
            item.Login?.Username ?? item.Identity?.Username,
            item.Login?.Password,
            item.Login?.Uris.Select(VaultUriDraft.FromUri).ToArray() is { Length: > 0 } uris
                ? uris
                : [VaultUriDraft.New()],
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
            ["folderId"] = EmptyToNull(FolderId),
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
                    ["uris"] = new JsonArray(Uris
                        .Where(uri => !string.IsNullOrWhiteSpace(uri.Value))
                        .Select(uri => (JsonNode)new JsonObject
                        {
                            ["uri"] = uri.Value.Trim(),
                            ["match"] = uri.Match
                        })
                        .ToArray())
                };
                break;
            case BitwardenItemType.Card:
                obj["card"] = new JsonObject
                {
                    ["cardholderName"] = EmptyToNull(CardholderName), ["brand"] = EmptyToNull(CardBrand),
                    ["number"] = EmptyToNull(CardNumber), ["expMonth"] = EmptyToNull(CardExpMonth),
                    ["expYear"] = EmptyToNull(CardExpYear), ["code"] = EmptyToNull(CardCode)
                };
                break;
            case BitwardenItemType.Identity:
                obj["identity"] = new JsonObject
                {
                    ["firstName"] = EmptyToNull(FirstName), ["lastName"] = EmptyToNull(LastName),
                    ["email"] = EmptyToNull(Email), ["phone"] = EmptyToNull(Phone),
                    ["company"] = EmptyToNull(Company), ["address1"] = EmptyToNull(Address)
                };
                break;
            case BitwardenItemType.SecureNote:
                obj["secureNote"] = new JsonObject { ["type"] = 0 };
                break;
        }

        return obj;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record VaultUriDraft(Guid Key, string Value, int? Match)
{
    public static VaultUriDraft New() => new(Guid.NewGuid(), string.Empty, null);

    public static VaultUriDraft FromUri(UriEntry uri) =>
        new(Guid.NewGuid(), uri.Uri ?? string.Empty, uri.Match);
}

public sealed record IconInfo(string? Glyph, string? ImageUrl)
{
    public bool IsGlyph => !string.IsNullOrWhiteSpace(Glyph);
    public static IconInfo FromGlyph(string glyph) => new(glyph, null);
    public static IconInfo FromUrl(string url) => new(null, url);
}
