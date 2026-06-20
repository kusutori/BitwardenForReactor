using System;
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
using WinUI = Microsoft.UI.Xaml.Controls;

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
                        TextBlock(secondary)
                            .Foreground(Theme.SecondaryText)
                            .TextTrimming(TextTrimming.CharacterEllipsis),
                        Props.IsTrashView && item.DeletedDate is not null
                            ? TextBlock($"已删除 · {item.DeletedDate:yyyy-MM-dd}")
                                .Foreground(Theme.SecondaryText)
                                .FontSize(11)
                            : null)
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
            .WithBorder(Props.IsSelected ? Theme.Accent : Theme.CardStroke, Props.IsSelected ? 2 : 0)
            .HorizontalAlignment(HorizontalAlignment.Stretch);
    }

    private Element BuildOpenButton(BitwardenItem item) =>
        CompactButton("\uE8A7", "前往项目网站",
                () => _ = AppCommands.OpenUriAsync(item.PrimaryUri, Props.Dispatch))
            .IsEnabled(!string.IsNullOrWhiteSpace(item.PrimaryUri))
            .ToolTip("前往网站");

    private Element BuildCopyMenu(BitwardenItem item)
    {
        var canCopy = !string.IsNullOrWhiteSpace(item.Username) || !string.IsNullOrWhiteSpace(item.Password);
        var anchor = CompactButton("\uE8C8", "打开复制菜单")
            .IsEnabled(canCopy)
            .ToolTip("复制");

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
        return CompactButton("\uE712", "打开更多操作菜单")
            .ToolTip("更多操作")
            .Set(button => button.Flyout = BuildMoreFlyout(item));
    }

    private WinUI.MenuFlyout BuildMoreFlyout(BitwardenItem item)
    {
        var flyout = new WinUI.MenuFlyout();
        if (Props.IsTrashView)
        {
            flyout.Items.Add(NativeMenuItem("恢复", "\uE7A7", () => _ = AppCommands.RestoreAsync(item, Props.Dispatch)));
            flyout.Items.Add(new WinUI.MenuFlyoutSeparator());
            flyout.Items.Add(NativeMenuItem("永久删除", "\uE74D", () => Props.Dispatch(new DeleteRequested(item, true)), critical: true));
            return flyout;
        }

        flyout.Items.Add(NativeMenuItem("前往", "\uE8A7", () => _ = AppCommands.OpenUriAsync(item.PrimaryUri, Props.Dispatch), !string.IsNullOrWhiteSpace(item.PrimaryUri)));
        flyout.Items.Add(NativeMenuItem("复制用户名", "\uE8C8", () => Copy(item.Username), !string.IsNullOrWhiteSpace(item.Username)));
        flyout.Items.Add(NativeMenuItem("复制密码", "\uE8C8", () => Copy(item.Password), !string.IsNullOrWhiteSpace(item.Password)));
        flyout.Items.Add(new WinUI.MenuFlyoutSeparator());
        flyout.Items.Add(NativeMenuItem(item.Favorite ? "取消收藏" : "收藏", "\uE735", () => _ = AppCommands.ToggleFavoriteAsync(item, Props.Dispatch)));
        flyout.Items.Add(NativeMenuItem("编辑", "\uE70F", () => Props.Dispatch(new EditorOpened(VaultItemDraft.FromItem(item)))));
        flyout.Items.Add(NativeMenuItem("附件", "\uE723", null, enabled: false));
        flyout.Items.Add(NativeMenuItem("克隆", "\uE8C8", () => _ = AppCommands.CloneItemAsync(item, Props.Dispatch)));
        flyout.Items.Add(NativeMenuItem("归档", "\uE8DE", null, enabled: false));
        flyout.Items.Add(new WinUI.MenuFlyoutSeparator());
        flyout.Items.Add(NativeMenuItem("删除", "\uE74D", () => Props.Dispatch(new DeleteRequested(item, false)), critical: true));
        return flyout;
    }

    private static ButtonElement CompactButton(string glyph, string automationName, Action? onClick = null) =>
        Button(Icon(FontIcon(glyph, fontSize: 12)), onClick)
            .SubtleButton()
            .Set(button =>
            {
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
                button.VerticalContentAlignment = VerticalAlignment.Center;
            })
            .Width(28)
            .Height(28)
            .MinWidth(0)
            .MinHeight(0)
            .Padding(0)
            .AutomationName(automationName);

    private static WinUI.MenuFlyoutItem NativeMenuItem(
        string text,
        string glyph,
        Action? onClick,
        bool enabled = true,
        bool critical = false)
    {
        var item = new WinUI.MenuFlyoutItem
        {
            Text = text,
            Icon = new WinUI.FontIcon { Glyph = glyph, FontSize = 14 },
            IsEnabled = enabled
        };
        if (onClick is not null)
        {
            item.Click += (_, _) => onClick();
        }
        if (critical)
        {
            item.Foreground = Microsoft.UI.Xaml.Application.Current.Resources[Theme.SystemCritical.ResourceKey] as Brush
                ?? new SolidColorBrush(Microsoft.UI.Colors.Red);
        }
        return item;
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
