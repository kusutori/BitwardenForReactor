using System;
using System.Globalization;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;

namespace BitwardenForReactor.Application;

public static class VaultDisplay
{
    public static string? Username(BitwardenItem item) => item.Login?.Username ?? item.Identity?.Username;

    public static string? PrimaryUri(BitwardenItem item) => item.Login?.Uris.FirstOrDefault()?.Uri;

    public static string TypeLabel(BitwardenItem item) => item.Type switch
    {
        BitwardenItemType.Login => T("Logins"),
        BitwardenItemType.SecureNote => T("Note"),
        BitwardenItemType.Card => T("Cards"),
        BitwardenItemType.Identity => T("Identities"),
        _ => T("Item")
    };

    public static string FilterTitle(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ActiveFolderId))
        {
            return state.Folders.FirstOrDefault(folder => folder.Id == state.ActiveFolderId)?.Name ?? T("Folders");
        }

        return state.Filter switch
        {
            VaultFilter.Logins => T("Logins"),
            VaultFilter.Cards => T("Cards"),
            VaultFilter.Identities => T("Identities"),
            VaultFilter.Notes => T("Secure notes"),
            VaultFilter.Favorites => T("Favorite"),
            VaultFilter.Archive => T("Archive"),
            VaultFilter.Trash => T("Trash"),
            _ => T("All items")
        };
    }

    public static string FilterDescription(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ActiveFolderId))
        {
            return T("Vault items in the current folder");
        }

        return state.Filter switch
        {
            VaultFilter.Logins => T("Website accounts, app accounts, and passkey-related items"),
            VaultFilter.Cards => T("Credit cards, debit cards, and payment information"),
            VaultFilter.Identities => T("Contacts, addresses, and identity information"),
            VaultFilter.Notes => T("Encrypted plain-text notes"),
            VaultFilter.Favorites => T("Frequently used items marked as favorites"),
            VaultFilter.Archive => T("Archived vault items"),
            VaultFilter.Trash => T("Deleted items that can still be restored"),
            _ => T("Browse every item in the current vault")
        };
    }

    public static string EmptyListTitle(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SearchQuery)) return T("No search results");

        return state.Filter switch
        {
            _ when !string.IsNullOrWhiteSpace(state.ActiveFolderId) => T("Folder is empty"),
            VaultFilter.Favorites => T("No favorites yet"),
            VaultFilter.Archive => T("Archive is empty"),
            VaultFilter.Trash => T("Trash is empty"),
            VaultFilter.Logins => T("No login items"),
            VaultFilter.Cards => T("No card items"),
            VaultFilter.Identities => T("No identity items"),
            VaultFilter.Notes => T("No secure notes"),
            _ => T("Vault is empty")
        };
    }

    public static string EmptyListDescription(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SearchQuery))
        {
            return T("Try another keyword, or clear the search box to view all items.");
        }

        return state.Filter switch
        {
            _ when !string.IsNullOrWhiteSpace(state.ActiveFolderId) => T("There are no items in this folder yet."),
            VaultFilter.Favorites => T("Items appear here after you mark them as favorites in the details pane or Bitwarden."),
            VaultFilter.Archive => T("Items appear here after you use Archive from the item menu."),
            VaultFilter.Trash => T("Deleted items are moved to Trash first, where you can restore or permanently delete them."),
            _ => T("Select New item in the title bar to get started.")
        };
    }

    public static string Mask(string kind) => kind switch
    {
        "cvv" => "***",
        "ssn" => "***-**-****",
        _ => "••••••••"
    };

    public static string? MaskCard(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        return number.Length > 4 ? $"•••• •••• •••• {number[^4..]}" : number;
    }

    public static string? FormatExpiry(CardData? card)
    {
        if (card is null || (string.IsNullOrWhiteSpace(card.ExpMonth) && string.IsNullOrWhiteSpace(card.ExpYear))) return null;
        return $"{card.ExpMonth}/{card.ExpYear}";
    }

    public static string? JoinParts(params string?[] parts)
    {
        var text = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static string FormatStatus(BitwardenStatus? status) => status is null
        ? T("Bitwarden CLI was not detected or its status is unavailable")
        : status.Status switch
        {
            "unlocked" => T("Unlocked · {server}", ("server", status.ServerUrl ?? T("Default server"))),
            "locked" => T("Locked · {server}", ("server", status.ServerUrl ?? T("Default server"))),
            "unauthenticated" => T("Not signed in"),
            _ => T("{status} · {server}", ("status", status.Status), ("server", status.ServerUrl ?? T("Default server")))
        };

    public static int ParsePositiveInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : fallback;
}
