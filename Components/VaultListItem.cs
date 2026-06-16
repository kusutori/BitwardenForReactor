using System;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Components;

public sealed record VaultListItemProps(BitwardenItem Item, bool IsSelected, bool IsTrashView);

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
                HStack(12,
                    Border(Icon(FontIcon(IconService.GetItemTypeGlyph(item.Type), fontSize: 17)))
                        .Width(36)
                        .Height(36)
                        .CornerRadius(18)
                        .Background(Theme.SubtleFill)
                        .VerticalAlignment(VerticalAlignment.Center),
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
                    .Flex(grow: 1, basis: 0))
                .VerticalAlignment(VerticalAlignment.Center))
            .Padding(left: 12, top: 9, right: 10, bottom: 9)
            .WithBorder(Props.IsSelected ? Theme.Accent : Theme.CardStroke, Props.IsSelected ? 2 : 0);
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
