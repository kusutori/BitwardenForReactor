using System;
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
        var folderItems = state.Folders
            .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(folder => NavItem(folder.Name, "Folder", FolderToTag(folder.Id)))
            .ToArray();

        return
        [
            NavItem("密码库", "Library", "VaultRoot") with
            {
                Children =
                [
                    NavItem("所有密码库", "Library", "AllVaults") with
                    {
                        Children = [NavItem("我的密码库", "Contact", "AllItems")]
                    },
                    NavItem("所有项目", "AllApps", "ItemTypes") with
                    {
                        Children =
                        [
                            NavItem("收藏夹", "Favorite", "Favorites"),
                            NavItem("登录", "World", "Logins"),
                            NavItem("支付卡", "ContactInfo", "Cards"),
                            NavItem("身份", "People", "Identities"),
                            NavItem("安全笔记", "Page", "Notes"),
                            NavItem("回收站", "Delete", "Trash")
                        ]
                    },
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

    private static string FilterToTag(VaultFilter filter) => filter switch
    {
        VaultFilter.Logins => "Logins",
        VaultFilter.Cards => "Cards",
        VaultFilter.Identities => "Identities",
        VaultFilter.Notes => "Notes",
        VaultFilter.Favorites => "Favorites",
        VaultFilter.Trash => "Trash",
        _ => "AllItems"
    };

    private static string FolderToTag(string folderId) => $"Folder:{folderId}";
}
