using System;
using System.Collections.Generic;
using BitwardenForReactor.Application;
using BitwardenForReactor.Components;
using BitwardenForReactor.Models;
using BitwardenForReactor.Pages;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;
using System.Linq;
using WinUI = Microsoft.UI.Xaml.Controls;

namespace BitwardenForReactor.Shell;

public sealed record BitwardenShellProps(
    AppState State,
    Action<AppAction> Dispatch,
    string MasterPassword,
    Action<string> SetMasterPassword);

public sealed class BitwardenShell : Component<BitwardenShellProps>
{
    public override Element Render()
    {
        var state = Props.State;
        return FlexColumn(
            TitleBar("BitwardenForReactor")
                .RightHeader(RenderTitleActions())
                .Flex(shrink: 0),
            state.HasNotice
                ? Component<AppNotice, AppNoticeProps>(new AppNoticeProps(state, Props.Dispatch))
                : null,
            state.IsBusy ? Component<BusyIndicator, AppState>(state) : null,
            state.IsUnlocked
                ? RenderMain()
                : Component<UnlockPage, UnlockPageProps>(
                        new UnlockPageProps(state, Props.Dispatch, Props.MasterPassword, Props.SetMasterPassword))
                    .WithKey(state.Settings.ActiveAccountId.ToString("D"))
                    .Flex(grow: 1, basis: 0));
    }

    private Element RenderTitleActions() =>
        HStack(8,
            ComboBox(
                    Props.State.Settings.Accounts.Select(account => account.DisplayName).ToArray(),
                    Math.Max(0, Props.State.Settings.Accounts.ToList().FindIndex(account => account.Id == Props.State.Settings.ActiveAccountId)),
                    index =>
                    {
                        if (index >= 0 && index < Props.State.Settings.Accounts.Count)
                        {
                            _ = AppCommands.SwitchAccountAsync(Props.State.Settings.Accounts[index].Id, Props.Dispatch);
                        }
                    })
                .Width(150)
                .AutomationName("当前账号"),
            Button("管理账号", () => Props.Dispatch(new AccountManagerVisibilityChanged(true)))
                .AutomationName("管理账号"),
            SplitButton("新建项目", () => OpenNewItem(BitwardenItemType.Login), MenuItems(
                    MenuItem("登录", () => OpenNewItem(BitwardenItemType.Login)),
                    MenuItem("安全笔记", () => OpenNewItem(BitwardenItemType.SecureNote)),
                    MenuItem("卡片", () => OpenNewItem(BitwardenItemType.Card)),
                    MenuItem("身份", () => OpenNewItem(BitwardenItemType.Identity)),
                    MenuSeparator(),
                    MenuItem("文件夹", () => Props.Dispatch(new FolderEditorOpened()))))
                .IsEnabled(Props.State.IsUnlocked && !Props.State.IsBusy)
                .AutomationName("新建项目"),
            Button("同步", () => _ = AppCommands.SyncAsync(Props.Dispatch))
                .IsEnabled(Props.State.IsUnlocked && !Props.State.IsBusy)
                .AutomationName("同步密码库"),
            Button("锁定", () => _ = AppCommands.LockAsync(Props.Dispatch))
                .IsEnabled(Props.State.IsUnlocked && !Props.State.IsBusy)
                .AutomationName("锁定密码库"));

    private void OpenNewItem(BitwardenItemType type) =>
        Props.Dispatch(new EditorOpened(VaultItemDraft.New(type)));

    private Element RenderMain()
    {
        var state = Props.State;
        var nav = NavigationView(
                VaultNavigation.BuildItems(state),
                state.ShowSettings
                    ? Component<SettingsPage, SettingsPageProps>(new SettingsPageProps(state, Props.Dispatch))
                    : Component<VaultPage, VaultPageProps>(new VaultPageProps(state, Props.Dispatch)))
            .PaneTitle("Bitwarden")
            .OpenPaneLength(260)
            .PaneDisplayMode(NavigationViewPaneDisplayMode.Left)
            .SelectedTagChanged(tag =>
            {
                if (tag is null)
                {
                    Props.Dispatch(new SettingsVisibilityChanged(true));
                    return;
                }

                if (VaultNavigation.TryGetFolderId(tag, out var folderId))
                {
                    Props.Dispatch(new FolderChanged(folderId));
                    return;
                }

                Props.Dispatch(new FilterChanged(VaultNavigation.TagToFilter(tag)));
            });

        return (nav with
        {
            SelectedTag = state.ShowSettings ? null : VaultNavigation.SelectedTag(state),
            IsSettingsVisible = true
        })
        .Set(native => AttachFolderEditButtons(native, state.Folders, Props.Dispatch))
        .Flex(grow: 1, basis: 0);
    }

    private static void AttachFolderEditButtons(
        WinUI.NavigationView navigationView,
        IReadOnlyList<BitwardenFolder> folders,
        Action<AppAction> dispatch)
    {
        var byId = folders.ToDictionary(folder => folder.Id, StringComparer.Ordinal);
        AttachFolderEditButtons(navigationView.MenuItems, byId, dispatch);
    }

    private static void AttachFolderEditButtons(
        System.Collections.IEnumerable items,
        IReadOnlyDictionary<string, BitwardenFolder> foldersById,
        Action<AppAction> dispatch)
    {
        foreach (var item in items)
        {
            if (item is not WinUI.NavigationViewItem navItem)
            {
                continue;
            }

            if (navItem.Tag is string tag && VaultNavigation.TryGetFolderId(tag, out var folderId) && foldersById.TryGetValue(folderId, out var folder))
            {
                navItem.Content = FolderNavContent(folder, dispatch);
            }

            if (navItem.MenuItems.Count > 0)
            {
                AttachFolderEditButtons(navItem.MenuItems, foldersById, dispatch);
            }
        }
    }

    private static UIElement FolderNavContent(BitwardenFolder folder, Action<AppAction> dispatch)
    {
        var grid = new WinUI.Grid
        {
            ColumnDefinitions =
            {
                new WinUI.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new WinUI.ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var name = new TextBlock
        {
            Text = folder.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        WinUI.Grid.SetColumn(name, 0);
        grid.Children.Add(name);

        var edit = new WinUI.Button
        {
            Content = new FontIcon { Glyph = "\uE70F", FontSize = 12 },
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        edit.Click += (_, _) => dispatch(new FolderEditorOpened(folder));
        WinUI.ToolTipService.SetToolTip(edit, "编辑文件夹");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(edit, $"编辑文件夹 {folder.Name}");
        WinUI.Grid.SetColumn(edit, 1);
        grid.Children.Add(edit);

        return grid;
    }
}
