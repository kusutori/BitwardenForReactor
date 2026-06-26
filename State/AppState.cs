using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using BitwardenForReactor.Application;
using Microsoft.UI.Xaml.Controls;

namespace BitwardenForReactor.State;

public sealed record AppState
{
    public BitwardenStatus? Status { get; init; }

    public IReadOnlyList<BitwardenItem> Items { get; init; } = [];

    public IReadOnlyList<BitwardenItem> TrashItems { get; init; } = [];

    public IReadOnlyList<BitwardenItem> ArchivedItems { get; init; } = [];

    public IReadOnlyList<BitwardenFolder> Folders { get; init; } = [];

    public VaultFilter Filter { get; init; } = VaultFilter.AllItems;

    public string? ActiveFolderId { get; init; }

    public string SearchQuery { get; init; } = string.Empty;

    public string? SelectedItemId { get; init; }

    public bool IsBusy { get; init; }

    public string BusyText { get; init; } = string.Empty;

    public AppSettings Settings { get; init; } = SettingsManager.Instance.Current;

    public IReadOnlyList<AccountSettings> Accounts => Settings.Accounts;

    public Guid ActiveAccountId => Settings.ActiveAccountId;

    public bool ShowSettings { get; init; }

    public bool ShowAccountManager { get; init; }

    public bool ShowFolderEditor { get; init; }

    public bool ShowGenerator { get; init; }

    public ImportExportDialogKind? ImportExportDialog { get; init; }

    public BitwardenFolder? FolderEditorTarget { get; init; }

    public VaultItemDraft? EditorDraft { get; init; }

    public BitwardenItem? DeleteTarget { get; init; }

    public bool DeletePermanently { get; init; }

    public string? NoticeTitle { get; init; }

    public string? NoticeMessage { get; init; }

    public InfoBarSeverity NoticeSeverity { get; init; } = InfoBarSeverity.Informational;

    public bool HasNotice => !string.IsNullOrWhiteSpace(NoticeTitle) || !string.IsNullOrWhiteSpace(NoticeMessage);

    public bool IsUnlocked => Status?.IsUnlocked == true || BitwardenApplicationService.Instance.IsUnlocked;

    public IReadOnlyList<BitwardenItem> VisibleItems
    {
        get
        {
            var source = Filter switch
            {
                VaultFilter.Trash => TrashItems,
                VaultFilter.Archive => ArchivedItems,
                _ => Items
            };
            var filtered = source.AsEnumerable();

            filtered = Filter switch
            {
                VaultFilter.Logins => filtered.Where(item => item.Type == BitwardenItemType.Login),
                VaultFilter.Cards => filtered.Where(item => item.Type == BitwardenItemType.Card),
                VaultFilter.Identities => filtered.Where(item => item.Type == BitwardenItemType.Identity),
                VaultFilter.Notes => filtered.Where(item => item.Type == BitwardenItemType.SecureNote),
                VaultFilter.Favorites => filtered.Where(item => item.Favorite),
                VaultFilter.Archive => filtered,
                VaultFilter.Trash => filtered,
                _ => filtered
            };

            if (!string.IsNullOrWhiteSpace(ActiveFolderId))
            {
                filtered = filtered.Where(item => item.FolderId == ActiveFolderId);
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.Trim();
                filtered = filtered.Where(item =>
                    Contains(item.Name, query) ||
                    Contains(VaultDisplay.Username(item), query) ||
                    Contains(VaultDisplay.PrimaryUri(item), query) ||
                    Contains(item.Notes, query));
            }

            return filtered
                .OrderByDescending(item => item.Favorite)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public BitwardenItem? SelectedItem =>
        VisibleItems.FirstOrDefault(item => item.Id == SelectedItemId) ??
        VisibleItems.FirstOrDefault();

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
}

public abstract record AppAction;

public enum ImportExportDialogKind
{
    Import,
    Export
}

public sealed record BusyChanged(bool IsBusy, string BusyText = "") : AppAction;

public sealed record StatusLoaded(BitwardenStatus? Status) : AppAction;

public sealed record VaultLoaded(
    IReadOnlyList<BitwardenItem> Items,
    IReadOnlyList<BitwardenItem> TrashItems,
    IReadOnlyList<BitwardenItem> ArchivedItems,
    IReadOnlyList<BitwardenFolder> Folders,
    string? SelectedItemId = null) : AppAction;

public sealed record FilterChanged(VaultFilter Filter) : AppAction;

public sealed record FolderChanged(string FolderId) : AppAction;

public sealed record SearchChanged(string Query) : AppAction;

public sealed record ItemSelected(string? ItemId) : AppAction;

public sealed record SettingsVisibilityChanged(bool Show) : AppAction;

public sealed record SettingsChanged(AppSettings Settings) : AppAction;

public sealed record SettingsSaved(AppSettings Settings) : AppAction;

public sealed record AccountsChanged(AppSettings Settings) : AppAction;

public sealed record AccountSwitched(AppSettings Settings) : AppAction;

public sealed record AccountManagerVisibilityChanged(bool Show) : AppAction;

public sealed record FolderEditorOpened(BitwardenFolder? Folder = null) : AppAction;

public sealed record FolderEditorClosed : AppAction;

public sealed record GeneratorVisibilityChanged(bool Show) : AppAction;

public sealed record ImportExportVisibilityChanged(ImportExportDialogKind? Kind) : AppAction;

public sealed record EditorOpened(VaultItemDraft Draft) : AppAction;

public sealed record EditorClosed : AppAction;

public sealed record DeleteRequested(BitwardenItem Item, bool Permanent) : AppAction;

public sealed record DeleteCancelled : AppAction;

public sealed record NoticeShown(string Title, string Message, InfoBarSeverity Severity) : AppAction;

public sealed record NoticeCleared : AppAction;

public sealed record Locked : AppAction;

public static class AppReducer
{
    public static AppState Reduce(AppState state, AppAction action) =>
        action switch
        {
            BusyChanged busy => state with { IsBusy = busy.IsBusy, BusyText = busy.BusyText },
            StatusLoaded loaded => state with { Status = loaded.Status },
            VaultLoaded loaded => state with
            {
                Items = loaded.Items,
                TrashItems = loaded.TrashItems,
                ArchivedItems = loaded.ArchivedItems,
                Folders = loaded.Folders,
                ActiveFolderId = PreserveFolderSelection(state.ActiveFolderId, loaded.Folders),
                SelectedItemId = loaded.SelectedItemId ?? PreserveSelection(state.SelectedItemId, loaded.Items, loaded.TrashItems, loaded.ArchivedItems, state.Filter)
            },
            FilterChanged changed => state with
            {
                Filter = changed.Filter,
                ActiveFolderId = null,
                ShowSettings = false,
                ShowGenerator = false,
                ImportExportDialog = null,
                SelectedItemId = null
            },
            FolderChanged changed => state with
            {
                Filter = VaultFilter.AllItems,
                ActiveFolderId = changed.FolderId,
                ShowSettings = false,
                ShowGenerator = false,
                ImportExportDialog = null,
                SelectedItemId = null
            },
            SearchChanged changed => state with { SearchQuery = changed.Query, SelectedItemId = null },
            ItemSelected selected => state with { SelectedItemId = selected.ItemId },
            SettingsVisibilityChanged changed => state with { ShowSettings = changed.Show, ShowGenerator = false, ImportExportDialog = null },
            SettingsChanged changed => state with { Settings = changed.Settings },
            SettingsSaved saved => state with { Settings = saved.Settings, ShowSettings = false },
            AccountsChanged changed => state with { Settings = changed.Settings },
            AccountManagerVisibilityChanged changed => state with { ShowAccountManager = changed.Show },
            FolderEditorOpened opened => state with { ShowFolderEditor = true, FolderEditorTarget = opened.Folder },
            FolderEditorClosed => state with { ShowFolderEditor = false, FolderEditorTarget = null },
            GeneratorVisibilityChanged changed => state with
            {
                ShowGenerator = changed.Show,
                ImportExportDialog = changed.Show ? null : state.ImportExportDialog,
                ShowSettings = changed.Show ? false : state.ShowSettings
            },
            ImportExportVisibilityChanged changed => state with
            {
                ImportExportDialog = changed.Kind,
                ShowGenerator = changed.Kind is null ? state.ShowGenerator : false,
                ShowSettings = changed.Kind is null ? state.ShowSettings : false
            },
            AccountSwitched switched => state with
            {
                Settings = switched.Settings,
                Status = null,
                Items = [],
                TrashItems = [],
                ArchivedItems = [],
                Folders = [],
                SelectedItemId = null,
                Filter = VaultFilter.AllItems,
                ActiveFolderId = null,
                SearchQuery = string.Empty,
                ShowSettings = false,
                ShowAccountManager = false,
                ShowFolderEditor = false,
                ShowGenerator = false,
                ImportExportDialog = null,
                FolderEditorTarget = null
            },
            EditorOpened opened => state with { EditorDraft = opened.Draft },
            EditorClosed => state with { EditorDraft = null },
            DeleteRequested requested => state with { DeleteTarget = requested.Item, DeletePermanently = requested.Permanent },
            DeleteCancelled => state with { DeleteTarget = null, DeletePermanently = false },
            NoticeShown shown => state with
            {
                NoticeTitle = shown.Title,
                NoticeMessage = shown.Message,
                NoticeSeverity = shown.Severity
            },
            NoticeCleared => state with { NoticeTitle = null, NoticeMessage = null },
            Locked => state with
            {
                Status = (state.Status ?? new BitwardenStatus()) with { Status = "locked" },
                Items = [],
                TrashItems = [],
                ArchivedItems = [],
                Folders = [],
                SelectedItemId = null,
                Filter = VaultFilter.AllItems,
                ActiveFolderId = null,
                SearchQuery = string.Empty,
                ShowSettings = false,
                ShowFolderEditor = false,
                ShowGenerator = false,
                ImportExportDialog = null,
                FolderEditorTarget = null
            },
            _ => state
        };

    private static string? PreserveSelection(
        string? selectedItemId,
        IReadOnlyList<BitwardenItem> items,
        IReadOnlyList<BitwardenItem> trashItems,
        IReadOnlyList<BitwardenItem> archivedItems,
        VaultFilter filter)
    {
        if (selectedItemId is null)
        {
            return null;
        }

        var source = filter switch
        {
            VaultFilter.Trash => trashItems,
            VaultFilter.Archive => archivedItems,
            _ => items
        };
        return source.Any(item => item.Id == selectedItemId) ? selectedItemId : null;
    }

    private static string? PreserveFolderSelection(string? activeFolderId, IReadOnlyList<BitwardenFolder> folders)
    {
        if (activeFolderId is null)
        {
            return null;
        }

        return folders.Any(folder => folder.Id == activeFolderId) ? activeFolderId : null;
    }
}
