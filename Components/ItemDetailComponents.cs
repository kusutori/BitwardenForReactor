using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.Application;
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
    bool IsArchiveView,
    Action Edit,
    Action Archive,
    Action Delete,
    Action PermanentDelete,
    Action Restore);

public sealed class DetailHeader : Component<DetailHeaderProps>
{
    public override Element Render()
    {
        var item = Props.Item;

        return Border(
                Grid(
                    columns: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                    rows: [GridSize.Auto],
                    Border(Component<ItemIcon, ItemIconProps>(new ItemIconProps(item, 32)))
                        .Width(52)
                        .Height(52)
                        .CornerRadius(8)
                        .Background(Theme.SubtleFill)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 0),
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
                            Border(TextBlock(VaultDisplay.TypeLabel(item)).Foreground(Theme.SecondaryText))
                                .Padding(left: 8, top: 3, right: 8, bottom: 3)
                                .CornerRadius(4)
                                .Background(Theme.SubtleFill),
                            item.DeletedDate is not null
                                ? TextBlock($"已删除 {item.DeletedDate:yyyy-MM-dd}").Foreground(Theme.SecondaryText)
                                : null,
                            item.ArchivedDate is not null
                                ? TextBlock($"已归档 {item.ArchivedDate:yyyy-MM-dd}").Foreground(Theme.SecondaryText)
                                : null))
                    .Margin(left: 16, right: 16)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(column: 1),
                    HStack(8,
                        Button(HStack(6,
                                Icon(FontIcon("\uE70F", fontSize: 14)),
                                TextBlock("编辑")), Props.Edit)
                            .SubtleButton()
                            .ToolTip("编辑")
                            .AutomationName("编辑项目"),
                        !Props.IsTrashView && !Props.IsArchiveView
                            ? Button(Icon(FontIcon("\uE7B8")), Props.Archive)
                                .SubtleButton()
                                .ToolTip("归档")
                                .AutomationName("归档项目")
                            : null,
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
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(column: 2)))
            .Padding(18)
            .CornerRadius(8)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .HorizontalAlignment(HorizontalAlignment.Stretch);
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
            .WithBorder(Theme.CardStroke, 1)
            .HorizontalAlignment(HorizontalAlignment.Stretch);

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
    Action<string>? CopyRequested,
    Element? ExtraAction = null);

public sealed class DetailFieldRow : Component<DetailFieldRowProps>
{
    public override Element Render() =>
        Border(
                Grid(
                    columns: [GridSize.Star(), GridSize.Auto],
                    rows: [GridSize.Auto],
                    VStack(4,
                        TextBlock(Props.Label).Foreground(Theme.SecondaryText),
                        TextBlock(Props.Value).TextWrapping())
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(column: 0),
                    HStack(4,
                            Props.ExtraAction,
                            BuildCopyButton())
                        .Margin(left: 12)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 1)))
            .Padding(left: 16, top: 12, right: 12, bottom: 12)
            .HorizontalAlignment(HorizontalAlignment.Stretch);

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
    public override Element Render()
    {
        var (revealed, setRevealed) = UseState(false);
        var value = revealed ? Props.Value ?? string.Empty : Props.MaskedValue;

        return Component<DetailFieldRow, DetailFieldRowProps>(
            new DetailFieldRowProps(
                Props.Label,
                value,
                Props.Value,
                Props.CopyRequested,
                Button(Icon(FontIcon(revealed ? "\uE890" : "\uED1A")), () => setRevealed(!revealed))
                    .SubtleButton()
                    .ToolTip(revealed ? "隐藏" : "显示")
                    .AutomationName($"{(revealed ? "隐藏" : "显示")}{Props.Label}")));
    }
}

public sealed record TotpFieldProps(Action CopyRequested);

public sealed class TotpField : Component<TotpFieldProps>
{
    public override Element Render() =>
        Border(
                Grid(
                    columns: [GridSize.Star(), GridSize.Auto],
                    rows: [GridSize.Auto],
                    VStack(4,
                        TextBlock("TOTP").Foreground(Theme.SecondaryText),
                        TextBlock("点击获取一次性验证码").TextWrapping())
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Grid(column: 0),
                    Button("获取", Props.CopyRequested)
                        .SubtleButton()
                        .AutomationName("复制TOTP")
                        .Margin(left: 12)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 1)))
            .Padding(left: 16, top: 12, right: 12, bottom: 12)
            .HorizontalAlignment(HorizontalAlignment.Stretch);
}
