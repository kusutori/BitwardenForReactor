using System;
using BitwardenForReactor.Application;
using BitwardenForReactor.Components;
using BitwardenForReactor.Models;
using BitwardenForReactor.Pages;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

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
                    .Flex(grow: 1, basis: 0));
    }

    private Element RenderTitleActions() =>
        HStack(8,
            Button("新建项目", () => Props.Dispatch(new EditorOpened(VaultItemDraft.New())))
                .IsEnabled(Props.State.IsUnlocked && !Props.State.IsBusy)
                .AutomationName("新建项目"),
            Button("同步", () => _ = AppCommands.SyncAsync(Props.Dispatch))
                .IsEnabled(Props.State.IsUnlocked && !Props.State.IsBusy)
                .AutomationName("同步密码库"),
            Button("锁定", () => _ = AppCommands.LockAsync(Props.Dispatch))
                .IsEnabled(Props.State.IsUnlocked && !Props.State.IsBusy)
                .AutomationName("锁定密码库"));

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
        }).Flex(grow: 1, basis: 0);
    }
}
