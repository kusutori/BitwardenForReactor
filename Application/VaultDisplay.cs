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
        BitwardenItemType.Login => T("登录"),
        BitwardenItemType.SecureNote => T("笔记"),
        BitwardenItemType.Card => T("卡片"),
        BitwardenItemType.Identity => T("身份"),
        _ => T("项目")
    };

    public static string FilterTitle(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ActiveFolderId))
        {
            return state.Folders.FirstOrDefault(folder => folder.Id == state.ActiveFolderId)?.Name ?? T("文件夹");
        }

        return state.Filter switch
        {
            VaultFilter.Logins => T("登录"),
            VaultFilter.Cards => T("卡片"),
            VaultFilter.Identities => T("身份"),
            VaultFilter.Notes => T("安全笔记"),
            VaultFilter.Favorites => T("收藏"),
            VaultFilter.Archive => T("归档"),
            VaultFilter.Trash => T("回收站"),
            _ => T("全部项目")
        };
    }

    public static string FilterDescription(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ActiveFolderId))
        {
            return T("当前文件夹中的密码库项目");
        }

        return state.Filter switch
        {
            VaultFilter.Logins => T("网站账号、应用账号和通行密钥相关项目"),
            VaultFilter.Cards => T("信用卡、借记卡和付款信息"),
            VaultFilter.Identities => T("联系人、地址和身份信息"),
            VaultFilter.Notes => T("加密保存的纯文本笔记"),
            VaultFilter.Favorites => T("标记为收藏的常用项目"),
            VaultFilter.Archive => T("已归档的密码库项目"),
            VaultFilter.Trash => T("已删除但仍可恢复的项目"),
            _ => T("浏览当前密码库中的全部项目")
        };
    }

    public static string EmptyListTitle(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SearchQuery)) return T("没有搜索结果");

        return state.Filter switch
        {
            _ when !string.IsNullOrWhiteSpace(state.ActiveFolderId) => T("文件夹为空"),
            VaultFilter.Favorites => T("还没有收藏项目"),
            VaultFilter.Archive => T("归档为空"),
            VaultFilter.Trash => T("回收站为空"),
            VaultFilter.Logins => T("没有登录项目"),
            VaultFilter.Cards => T("没有卡片项目"),
            VaultFilter.Identities => T("没有身份项目"),
            VaultFilter.Notes => T("没有安全笔记"),
            _ => T("密码库为空")
        };
    }

    public static string EmptyListDescription(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SearchQuery))
        {
            return T("换一个关键词，或清空搜索框后查看全部项目。");
        }

        return state.Filter switch
        {
            _ when !string.IsNullOrWhiteSpace(state.ActiveFolderId) => T("这个文件夹下还没有项目。"),
            VaultFilter.Favorites => T("在详情页或 Bitwarden 中为项目加星标后会显示在这里。"),
            VaultFilter.Archive => T("使用项目菜单中的归档操作后，项目会显示在这里。"),
            VaultFilter.Trash => T("删除的项目会先进入回收站，可以在这里恢复或永久删除。"),
            _ => T("可以点击标题栏的新建项目开始添加。")
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
        ? T("未检测到 Bitwarden CLI 或状态不可用")
        : status.Status switch
        {
            "unlocked" => T("已解锁 · {server}", ("server", status.ServerUrl ?? T("默认服务器"))),
            "locked" => T("已锁定 · {server}", ("server", status.ServerUrl ?? T("默认服务器"))),
            "unauthenticated" => T("尚未登录"),
            _ => T("{status} · {server}", ("status", status.Status), ("server", status.ServerUrl ?? T("默认服务器")))
        };

    public static int ParsePositiveInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : fallback;
}
