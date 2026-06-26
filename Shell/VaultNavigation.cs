using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Shell;

public static class VaultNavigation
{
    public static NavigationViewItemData[] BuildItems(AppState state)
    {
        var folderItems = BuildFolderItems(state.Folders);

        return
        [
            NavItem("密码库", "Library", "VaultRoot") with
            {
                Children =
                [
                    NavItem("所有密码库", "\uE8A9", "AllVaults") with
                    {
                        Children = [NavItem("我的密码库", "Contact", "AllItems")]
                    },
                    NavItem("所有项目", "\uE8A9", "ItemTypes") with
                    {
                        Children =
                        [
                            NavItem("收藏夹", "Favorite", "Favorites"),
                            NavItem("登录", "World", "Logins"),
                            NavItem("支付卡", "\uE8C7", "Cards"),
                            NavItem("身份", "People", "Identities"),
                            NavItem("安全笔记", "\uE70B", "Notes")
                        ]
                    },
                    NavItem("归档", "\uE7B8", "Archive"),
                    NavItem("回收站", "Delete", "Trash"),
                    NavItem("文件夹", "Folder", "Folders") with
                    {
                        Children = folderItems.Length > 0
                            ? folderItems
                            : [NavItem("暂无文件夹", "Folder", "FoldersEmpty")]
                    }
                ]
            }
        ];
    }

    public static VaultFilter TagToFilter(string tag) => tag switch
    {
        "Logins" => VaultFilter.Logins,
        "Cards" => VaultFilter.Cards,
        "Identities" => VaultFilter.Identities,
        "Notes" => VaultFilter.Notes,
        "Favorites" => VaultFilter.Favorites,
        "Archive" => VaultFilter.Archive,
        "Trash" => VaultFilter.Trash,
        _ => VaultFilter.AllItems
    };

    public static string SelectedTag(AppState state) =>
        !string.IsNullOrWhiteSpace(state.ActiveFolderId)
            ? FolderToTag(state.ActiveFolderId)
            : FilterToTag(state.Filter);

    public static bool TryGetFolderId(string? tag, out string folderId)
    {
        const string prefix = "Folder:";
        if (!string.IsNullOrWhiteSpace(tag) && tag.StartsWith(prefix, StringComparison.Ordinal))
        {
            folderId = tag[prefix.Length..];
            return !string.IsNullOrWhiteSpace(folderId);
        }

        folderId = string.Empty;
        return false;
    }

    public static bool IsFolderGroupTag(string? tag) =>
        !string.IsNullOrWhiteSpace(tag) && tag.StartsWith("FolderGroup:", StringComparison.Ordinal);

    private static string FilterToTag(VaultFilter filter) => filter switch
    {
        VaultFilter.Logins => "Logins",
        VaultFilter.Cards => "Cards",
        VaultFilter.Identities => "Identities",
        VaultFilter.Notes => "Notes",
        VaultFilter.Favorites => "Favorites",
        VaultFilter.Archive => "Archive",
        VaultFilter.Trash => "Trash",
        _ => "AllItems"
    };

    private static string FolderToTag(string folderId) => $"Folder:{folderId}";

    private static string FolderGroupToTag(string path) => $"FolderGroup:{path}";

    private static NavigationViewItemData[] BuildFolderItems(IReadOnlyList<BitwardenFolder> folders)
    {
        if (folders.Count == 0)
        {
            return [];
        }

        var root = new FolderNode(string.Empty);
        foreach (var folder in folders.OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var segments = FolderSegments(folder.Name);
            var current = root;
            foreach (var segment in segments)
            {
                if (!current.Children.TryGetValue(segment, out var child))
                {
                    child = new FolderNode(segment);
                    current.Children.Add(segment, child);
                }

                current = child;
            }

            current.Folder = folder;
        }

        return BuildFolderItems(root.Children.Values, string.Empty);
    }

    private static NavigationViewItemData[] BuildFolderItems(IEnumerable<FolderNode> nodes, string parentPath) =>
        nodes
            .OrderBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(node =>
            {
                var path = string.IsNullOrWhiteSpace(parentPath) ? node.Name : $"{parentPath}/{node.Name}";
                var children = node.Children.Count > 0 ? BuildFolderItems(node.Children.Values, path) : null;
                return NavItem(node.Name, "Folder", node.Folder is { } folder ? FolderToTag(folder.Id) : FolderGroupToTag(path)) with
                {
                    Children = children
                };
            })
            .ToArray();

    private static string[] FolderSegments(string name)
    {
        var segments = name
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return segments.Length > 0 ? segments : [name];
    }

    private sealed class FolderNode(string name)
    {
        public string Name { get; } = name;

        public BitwardenFolder? Folder { get; set; }

        public Dictionary<string, FolderNode> Children { get; } = new(StringComparer.CurrentCultureIgnoreCase);
    }
}
