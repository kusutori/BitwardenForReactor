using System;
using System.Collections.Generic;
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
                HStack(14,
                    Border(Icon(FontIcon(IconService.GetItemTypeGlyph(item.Type), fontSize: 24)))
                        .Width(48)
                        .Height(48)
                        .CornerRadius(24)
                        .Background(Theme.SubtleFill),
                    VStack(3,
                        HStack(8,
                            TextBlock(item.Name)
                                .SemiBold()
                                .TextTrimming(TextTrimming.CharacterEllipsis)
                                .Flex(grow: 1, basis: 0),
                            item.Favorite
                                ? Icon(FontIcon("\uE735", fontSize: 16)).Foreground(Theme.SystemCaution)
                                : null),
                        HStack(8,
                            TextBlock(item.TypeLabel).Foreground(Theme.SecondaryText),
                            item.DeletedDate is not null
                                ? TextBlock($"已删除 {item.DeletedDate:yyyy-MM-dd}").Foreground(Theme.SecondaryText)
                                : null))
                    .Flex(grow: 1, basis: 0),
                    Button(Icon(FontIcon("\uE70F")), Props.Edit)
                        .ToolTip("编辑")
                        .AutomationName("编辑项目"),
                    Props.IsTrashView
                        ? Button(Icon(FontIcon("\uE845")), Props.Restore)
                            .ToolTip("恢复")
                            .AutomationName("恢复项目")
                        : Button(Icon(FontIcon("\uE74D")), Props.Delete)
                            .ToolTip("删除")
                            .AutomationName("删除项目"),
                    Props.IsTrashView
                        ? Button(Icon(FontIcon("\uE74D")), Props.PermanentDelete)
                            .ToolTip("永久删除")
                            .AutomationName("永久删除项目")
                        : null)
                .VerticalAlignment(VerticalAlignment.Center))
            .Padding(left: 18, top: 14, right: 18, bottom: 14)
            .Background(Theme.LayerFill)
            .WithBorder(Theme.DividerStroke, 1);
    }
}

public sealed record DetailSectionProps(string Title, IReadOnlyList<Element> Children);

public sealed class DetailSection : Component<DetailSectionProps>
{
    public override Element Render() =>
        VStack(8,
            TextBlock(Props.Title).SemiBold(),
            VStack(0, [.. Props.Children]));
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
                    VStack(3,
                        TextBlock(Props.Label).Foreground(Theme.SecondaryText),
                        TextBlock(Props.Value).TextWrapping())
                    .Flex(grow: 1, basis: 0),
                    BuildCopyButton()))
            .Padding(left: 12, top: 9, right: 10, bottom: 9)
            .WithBorder(Theme.DividerStroke, 1);

    private Element BuildCopyButton() =>
        string.IsNullOrWhiteSpace(Props.CopyValue) || Props.CopyRequested is null
            ? Empty()
            : Button(Icon(FontIcon("\uE8C8")), () => Props.CopyRequested(Props.CopyValue))
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
                    VStack(3,
                        TextBlock("TOTP").Foreground(Theme.SecondaryText),
                        TextBlock("点击获取一次性验证码").TextWrapping())
                    .Flex(grow: 1, basis: 0),
                    Button("获取", Props.CopyRequested)
                        .AutomationName("复制TOTP")))
            .Padding(left: 12, top: 9, right: 10, bottom: 9)
            .WithBorder(Theme.DividerStroke, 1);
}
