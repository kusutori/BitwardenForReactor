using System;
using System.Collections.Generic;
using BitwardenForReactor.Application;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Components;

public sealed record VaultListItemProps(
    BitwardenItem Item,
    bool IsSelected,
    bool IsTrashView,
    Action<AppAction> Dispatch);

public sealed class VaultListItem : Component<VaultListItemProps>
{
    public override Element Render()
    {
        var item = Props.Item;
        var secondary = BuildSecondaryText(item);
        var stateText = Props.IsTrashView && item.DeletedDate is not null
            ? $"已删除 · {item.DeletedDate:yyyy-MM-dd}"
            : item.TypeLabel;

        return Border(
                Grid(
                    columns: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                    rows: [GridSize.Auto],
                    Border(Icon(FontIcon(IconService.GetItemTypeGlyph(item.Type), fontSize: 17)))
                        .Width(36)
                        .Height(36)
                        .CornerRadius(18)
                        .Background(Theme.SubtleFill)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 0),
                    VStack(3,
                        HStack(6,
                            TextBlock(item.Name)
                                .TextTrimming(TextTrimming.CharacterEllipsis)
                                .SemiBold()
                                .Flex(grow: 1, basis: 0),
                            item.Favorite
                                ? Icon(FontIcon("\uE735", fontSize: 13)).Foreground(Theme.SystemCaution)
                                : null),
                        HStack(8,
                            TextBlock(secondary)
                                .Foreground(Theme.SecondaryText)
                                .TextTrimming(TextTrimming.CharacterEllipsis)
                                .Flex(grow: 1, basis: 0),
                            Border(TextBlock(stateText).Foreground(Theme.SecondaryText))
                                .Padding(left: 6, top: 2, right: 6, bottom: 2)
                                .CornerRadius(4)
                                .Background(Theme.SubtleFill)))
                    .Margin(left: 12, right: 8)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(column: 1),
                    HStack(2,
                        BuildOpenButton(item),
                        BuildCopyMenu(item),
                        BuildMoreMenu(item))
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(column: 2)))
            .Padding(left: 12, top: 9, right: 10, bottom: 9)
            .WithBorder(Props.IsSelected ? Theme.Accent : Theme.CardStroke, Props.IsSelected ? 2 : 0);
    }

    private Element BuildOpenButton(BitwardenItem item) =>
        Button(Icon(FontIcon("\uE8A7", fontSize: 14)),
                () => _ = AppCommands.OpenUriAsync(item.PrimaryUri, Props.Dispatch))
            .SubtleButton()
            .Width(32)
            .Height(32)
            .IsEnabled(!string.IsNullOrWhiteSpace(item.PrimaryUri))
            .ToolTip("前往网站")
            .AutomationName("前往项目网站");

    private Element BuildCopyMenu(BitwardenItem item)
    {
        var canCopy = !string.IsNullOrWhiteSpace(item.Username) || !string.IsNullOrWhiteSpace(item.Password);
        var anchor = Button(Icon(FontIcon("\uE8C8", fontSize: 14)))
            .SubtleButton()
            .Width(32)
            .Height(32)
            .IsEnabled(canCopy)
            .ToolTip("复制")
            .AutomationName("打开复制菜单");

        return MenuFlyout(
            anchor,
            MenuItem("复制用户名", () => Copy(item.Username), icon: "\uE8C8") with
            {
                IsEnabled = !string.IsNullOrWhiteSpace(item.Username)
            },
            MenuItem("复制密码", () => Copy(item.Password), icon: "\uE8C8") with
            {
                IsEnabled = !string.IsNullOrWhiteSpace(item.Password)
            });
    }

    private Element BuildMoreMenu(BitwardenItem item)
    {
        var anchor = Button(Icon(FontIcon("\uE712", fontSize: 14)))
            .SubtleButton()
            .Width(32)
            .Height(32)
            .ToolTip("更多操作")
            .AutomationName("打开更多操作菜单");

        return MenuFlyout(anchor, BuildMoreItems(item).ToArray());
    }

    private List<MenuFlyoutItemBase> BuildMoreItems(BitwardenItem item)
    {
        if (Props.IsTrashView)
        {
            return
            [
                MenuItem("恢复", () => { _ = AppCommands.RestoreAsync(item, Props.Dispatch); }, icon: "\uE7A7"),
                MenuSeparator(),
                MenuItem("永久删除", () => Props.Dispatch(new DeleteRequested(item, true)), icon: "\uE74D")
            ];
        }

        return
        [
            MenuItem("前往", () => { _ = AppCommands.OpenUriAsync(item.PrimaryUri, Props.Dispatch); }, icon: "\uE8A7") with
            {
                IsEnabled = !string.IsNullOrWhiteSpace(item.PrimaryUri)
            },
            MenuItem("复制用户名", () => Copy(item.Username), icon: "\uE8C8") with
            {
                IsEnabled = !string.IsNullOrWhiteSpace(item.Username)
            },
            MenuItem("复制密码", () => Copy(item.Password), icon: "\uE8C8") with
            {
                IsEnabled = !string.IsNullOrWhiteSpace(item.Password)
            },
            MenuSeparator(),
            MenuItem(item.Favorite ? "取消收藏" : "收藏", () =>
            {
                _ = AppCommands.ToggleFavoriteAsync(item, Props.Dispatch);
            }, icon: "\uE735"),
            MenuItem("编辑", () => Props.Dispatch(new EditorOpened(VaultItemDraft.FromItem(item))), icon: "\uE70F"),
            MenuItem("附件", icon: "\uE723") with
            {
                IsEnabled = false,
                Description = "暂未支持附件管理"
            },
            MenuItem("克隆", () => { _ = AppCommands.CloneItemAsync(item, Props.Dispatch); }, icon: "\uE8C8"),
            MenuItem("归档", icon: "\uE8DE") with
            {
                IsEnabled = false,
                Description = "Bitwarden CLI 暂无归档能力"
            },
            MenuSeparator(),
            MenuItem("删除", () => Props.Dispatch(new DeleteRequested(item, false)), icon: "\uE74D")
        ];
    }

    private void Copy(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _ = AppCommands.CopyAsync(value, Props.Dispatch);
        }
    }

    private static string BuildSecondaryText(BitwardenItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Username))
        {
            return item.Username;
        }

        if (!string.IsNullOrWhiteSpace(item.PrimaryUri))
        {
            return TryGetHost(item.PrimaryUri) ?? item.PrimaryUri;
        }

        if (!string.IsNullOrWhiteSpace(item.Notes))
        {
            return item.Notes.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
        }

        return "无附加信息";
    }

    private static string? TryGetHost(string uriText)
    {
        if (!uriText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !uriText.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            uriText = $"https://{uriText}";
        }

        return Uri.TryCreate(uriText, UriKind.Absolute, out var uri) ? uri.Host : null;
    }
}
