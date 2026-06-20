using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Components;

public sealed record DetailHeaderProps(
    BitwardenItem Item,
    bool IsTrashView,
    Action Edit,
    Action Delete,
    Action PermanentDelete,
    Action Restore);

public sealed class DetailHeader : Component<DetailHeaderProps>
{
    public override Element Render()
    {
        var item = Props.Item;

        return Border(
                HStack(16,
                    Border(Icon(FontIcon(IconService.GetItemTypeGlyph(item.Type), fontSize: 26)))
                        .Width(52)
                        .Height(52)
                        .CornerRadius(8)
                        .Background(Theme.SubtleFill)
                        .VerticalAlignment(VerticalAlignment.Center),
                    VStack(6,
                        HStack(8,
                            TextBlock(item.Name)
                                .FontSize(22)
                                .SemiBold()
                                .TextTrimming(TextTrimming.CharacterEllipsis),
                            item.Favorite
                                ? Icon(FontIcon("\uE735", fontSize: 16)).Foreground(Theme.SystemCaution)
                                : null),
                        HStack(8,
                            Border(TextBlock(item.TypeLabel).Foreground(Theme.SecondaryText))
                                .Padding(left: 8, top: 3, right: 8, bottom: 3)
                                .CornerRadius(4)
                                .Background(Theme.SubtleFill),
                            item.DeletedDate is not null
                                ? TextBlock($"已删除 {item.DeletedDate:yyyy-MM-dd}").Foreground(Theme.SecondaryText)
                                : null))
                    .Flex(grow: 1, basis: 0),
                    HStack(8,
                        Button(HStack(6,
                                Icon(FontIcon("\uE70F", fontSize: 14)),
                                TextBlock("编辑")), Props.Edit)
                            .SubtleButton()
                            .ToolTip("编辑")
                            .AutomationName("编辑项目"),
                        Props.IsTrashView
                            ? Button(Icon(FontIcon("\uE845")), Props.Restore)
                                .SubtleButton()
                                .ToolTip("恢复")
                                .AutomationName("恢复项目")
                            : Button(Icon(FontIcon("\uE74D")), Props.Delete)
                                .SubtleButton()
                                .ToolTip("删除")
                                .AutomationName("删除项目"),
                        Props.IsTrashView
                            ? Button(Icon(FontIcon("\uE74D")), Props.PermanentDelete)
                                .SubtleButton()
                                .ToolTip("永久删除")
                                .AutomationName("永久删除项目")
                            : null)
                    .VerticalAlignment(VerticalAlignment.Center))
                .VerticalAlignment(VerticalAlignment.Center))
            .Padding(18)
            .CornerRadius(8)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1);
    }
}

public sealed record DetailSectionProps(string Title, IReadOnlyList<Element> Children);

public sealed class DetailSection : Component<DetailSectionProps>
{
    public override Element Render() =>
        Border(
                VStack(0,
                    Border(TextBlock(Props.Title).SemiBold())
                        .Padding(left: 16, top: 13, right: 16, bottom: 12),
                    Border(VStack()).Height(1).Background(Theme.DividerStroke),
                    VStack(0, BuildRows())))
            .CornerRadius(8)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1);

    private Element[] BuildRows() =>
        Props.Children
            .SelectMany((child, index) => index == 0
                ? [child]
                : new Element[]
                {
                    Border(VStack()).Height(1).Background(Theme.DividerStroke),
                    child
                })
            .ToArray();
}

public sealed record DetailFieldRowProps(
    string Label,
    string Value,
    string? CopyValue,
    Action<string>? CopyRequested);

public sealed class DetailFieldRow : Component<DetailFieldRowProps>
{
    public override Element Render() =>
        Border(
                HStack(12,
                    VStack(4,
                        TextBlock(Props.Label).Foreground(Theme.SecondaryText),
                        TextBlock(Props.Value).TextWrapping())
                    .Flex(grow: 1, basis: 0),
                    BuildCopyButton())
                .VerticalAlignment(VerticalAlignment.Center))
            .Padding(left: 16, top: 12, right: 12, bottom: 12);

    private Element BuildCopyButton() =>
        string.IsNullOrWhiteSpace(Props.CopyValue) || Props.CopyRequested is null
            ? Empty()
            : Button(Icon(FontIcon("\uE8C8")), () => Props.CopyRequested(Props.CopyValue))
                .SubtleButton()
                .ToolTip("复制")
                .AutomationName($"复制{Props.Label}");
}

public sealed record SensitiveFieldProps(
    string Label,
    string MaskedValue,
    string? Value,
    Action<string> CopyRequested);

public sealed class SensitiveField : Component<SensitiveFieldProps>
{
    public override Element Render() =>
        Component<DetailFieldRow, DetailFieldRowProps>(
            new DetailFieldRowProps(Props.Label, Props.MaskedValue, Props.Value, Props.CopyRequested));
}

public sealed record TotpFieldProps(Action CopyRequested);

public sealed class TotpField : Component<TotpFieldProps>
{
    public override Element Render() =>
        Border(
                HStack(12,
                    VStack(4,
                        TextBlock("TOTP").Foreground(Theme.SecondaryText),
                        TextBlock("点击获取一次性验证码").TextWrapping())
                    .Flex(grow: 1, basis: 0),
                    Button("获取", Props.CopyRequested)
                        .SubtleButton()
                        .AutomationName("复制TOTP")))
            .Padding(left: 16, top: 12, right: 12, bottom: 12);
}
