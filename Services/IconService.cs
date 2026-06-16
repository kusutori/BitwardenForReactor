using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BitwardenForReactor.Models;

namespace BitwardenForReactor.Services;

public static partial class IconService
{
    private const string IconServiceBaseUrl = "https://icons.bitwarden.net";
    private const int IconCheckTimeoutMs = 10000;
    private const int MaxCacheSize = 500;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMilliseconds(IconCheckTimeoutMs)
    };

    private static readonly ConcurrentDictionary<string, IconInfo> IconCache = new();
    private static readonly ConcurrentDictionary<string, bool> UnavailableDomains = new();

    public static readonly IconInfo DefaultLoginIcon = IconInfo.FromGlyph("\uE77B");
    public static readonly IconInfo DefaultWebIcon = IconInfo.FromGlyph("\uE774");
    public static readonly IconInfo DefaultCardIcon = IconInfo.FromGlyph("\uE8C7");
    public static readonly IconInfo DefaultIdentityIcon = IconInfo.FromGlyph("\uE779");
    public static readonly IconInfo DefaultSecureNoteIcon = IconInfo.FromGlyph("\uE70B");
    public static readonly IconInfo DefaultIcon = IconInfo.FromGlyph("\uE72E");

    public static IconInfo GetItemIcon(BitwardenItem item)
    {
        if (item.Type != BitwardenItemType.Login)
        {
            return GetDefaultIcon(item.Type);
        }

        var domain = ExtractDomainFromItem(item);
        if (string.IsNullOrWhiteSpace(domain))
        {
            return DefaultWebIcon;
        }

        if (IconCache.TryGetValue(domain, out var cachedIcon))
        {
            return cachedIcon;
        }

        return IconInfo.FromUrl($"{IconServiceBaseUrl}/{domain}/icon.png");
    }

    public static async Task<IconInfo> GetItemIconAsync(BitwardenItem item, CancellationToken cancellationToken = default)
    {
        if (item.Type != BitwardenItemType.Login)
        {
            return GetDefaultIcon(item.Type);
        }

        var domain = ExtractDomainFromItem(item);
        if (string.IsNullOrWhiteSpace(domain))
        {
            return DefaultWebIcon;
        }

        if (IconCache.TryGetValue(domain, out var cachedIcon))
        {
            return cachedIcon;
        }

        if (UnavailableDomains.ContainsKey(domain))
        {
            return DefaultWebIcon;
        }

        var iconUrl = $"{IconServiceBaseUrl}/{domain}/icon.png";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(IconCheckTimeoutMs);
            using var request = new HttpRequestMessage(HttpMethod.Head, iconUrl);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var icon = IconInfo.FromUrl(iconUrl);
                CacheIcon(domain, icon);
                return icon;
            }
        }
        catch
        {
            // Fall back to glyphs. The UI remains usable when icons.bitwarden.net is unavailable.
        }

        UnavailableDomains.TryAdd(domain, true);
        CacheIcon(domain, DefaultWebIcon);
        return DefaultWebIcon;
    }

    public static IconInfo GetDefaultIcon(BitwardenItemType type) =>
        type switch
        {
            BitwardenItemType.Login => DefaultWebIcon,
            BitwardenItemType.Card => DefaultCardIcon,
            BitwardenItemType.Identity => DefaultIdentityIcon,
            BitwardenItemType.SecureNote => DefaultSecureNoteIcon,
            _ => DefaultIcon
        };

    public static string GetItemTypeGlyph(BitwardenItemType type) =>
        GetDefaultIcon(type).Glyph ?? DefaultIcon.Glyph!;

    public static void ClearCache()
    {
        IconCache.Clear();
        UnavailableDomains.Clear();
    }

    private static void CacheIcon(string domain, IconInfo icon)
    {
        if (IconCache.Count >= MaxCacheSize)
        {
            foreach (var key in IconCache.Keys.Take(MaxCacheSize / 2))
            {
                IconCache.TryRemove(key, out _);
            }
        }

        IconCache.TryAdd(domain, icon);
    }

    private static string? ExtractDomainFromItem(BitwardenItem item)
    {
        var uri = item.Login?.Uris?.FirstOrDefault()?.Uri;
        return string.IsNullOrWhiteSpace(uri) ? null : ExtractHostname(uri);
    }

    private static string? ExtractHostname(string uriString)
    {
        if (!uriString.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !uriString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!uriString.Contains("://") && !uriString.StartsWith("android", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractDomainFromHostname(uriString.Split('/')[0]);
            }

            return null;
        }

        return Uri.TryCreate(uriString, UriKind.Absolute, out var uri)
            ? ExtractDomainFromHostname(uri.Host)
            : null;
    }

    private static string? ExtractDomainFromHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return null;
        }

        hostname = hostname.ToLowerInvariant().Trim();
        if (IpAddressRegex().IsMatch(hostname) || hostname == "localhost")
        {
            return hostname == "localhost" ? null : hostname;
        }

        var parts = hostname.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var twoPartTlds = new[]
        {
            "co.uk", "co.jp", "co.kr", "co.nz", "co.za", "co.in",
            "com.au", "com.br", "com.cn", "com.hk", "com.mx", "com.sg", "com.tw",
            "org.uk", "net.au", "gov.uk", "ac.uk", "edu.au"
        };

        if (parts.Length >= 3 && twoPartTlds.Contains($"{parts[^2]}.{parts[^1]}"))
        {
            return $"{parts[^3]}.{parts[^2]}.{parts[^1]}";
        }

        return $"{parts[^2]}.{parts[^1]}";
    }

    [GeneratedRegex(@"^(\d{1,3}\.){3}\d{1,3}$")]
    private static partial Regex IpAddressRegex();
}
