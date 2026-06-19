using System;
using System.Globalization;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;

namespace BitwardenForReactor.Application;

public static class VaultDisplay
{
    public static string FilterTitle(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ActiveFolderId))
        {
            return state.Folders.FirstOrDefault(folder => folder.Id == state.ActiveFolderId)?.Name ?? "文件夹";
        }

        return state.Filter switch
        {
            VaultFilter.Logins => "登录",
            VaultFilter.Cards => "卡片",
            VaultFilter.Identities => "身份",
            VaultFilter.Notes => "安全笔记",
            VaultFilter.Favorites => "收藏",
            VaultFilter.Trash => "回收站",
            _ => "全部项目"
        };
    }

    public static string FilterDescription(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ActiveFolderId))
        {
            return "当前文件夹中的密码库项目";
        }

        return state.Filter switch
        {
            VaultFilter.Logins => "网站账号、应用账号和通行密钥相关项目",
            VaultFilter.Cards => "信用卡、借记卡和付款信息",
            VaultFilter.Identities => "联系人、地址和身份信息",
            VaultFilter.Notes => "加密保存的纯文本笔记",
            VaultFilter.Favorites => "标记为收藏的常用项目",
            VaultFilter.Trash => "已删除但仍可恢复的项目",
            _ => "浏览当前密码库中的全部项目"
        };
    }

    public static string EmptyListTitle(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SearchQuery)) return "没有搜索结果";

        return state.Filter switch
        {
            _ when !string.IsNullOrWhiteSpace(state.ActiveFolderId) => "文件夹为空",
            VaultFilter.Favorites => "还没有收藏项目",
            VaultFilter.Trash => "回收站为空",
            VaultFilter.Logins => "没有登录项目",
            VaultFilter.Cards => "没有卡片项目",
            VaultFilter.Identities => "没有身份项目",
            VaultFilter.Notes => "没有安全笔记",
            _ => "密码库为空"
        };
    }

    public static string EmptyListDescription(AppState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SearchQuery))
        {
            return "换一个关键词，或清空搜索框后查看全部项目。";
        }

        return state.Filter switch
        {
            _ when !string.IsNullOrWhiteSpace(state.ActiveFolderId) => "这个文件夹下还没有项目。",
            VaultFilter.Favorites => "在详情页或 Bitwarden 中为项目加星标后会显示在这里。",
            VaultFilter.Trash => "删除的项目会先进入回收站，可以在这里恢复或永久删除。",
            _ => "可以点击标题栏的新建项目开始添加。"
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
        ? "未检测到 Bitwarden CLI 或状态不可用"
        : status.Status switch
        {
            "unlocked" => $"已解锁 · {status.ServerUrl ?? "默认服务器"}",
            "locked" => $"已锁定 · {status.ServerUrl ?? "默认服务器"}",
            "unauthenticated" => "尚未登录，请先在终端执行 bw login",
            _ => $"{status.Status} · {status.ServerUrl ?? "默认服务器"}"
        };

    public static int ParsePositiveInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : fallback;
}
