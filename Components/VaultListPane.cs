using System;
using System.Linq;
using BitwardenForReactor.Application;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Components;

public sealed record VaultPaneProps(AppState State, Action<AppAction> Dispatch);

public sealed class VaultListPane : Component<VaultPaneProps>
{
    public override Element Render()
    {
        var state = Props.State;
        var items = state.VisibleItems;
        var selectedId = state.SelectedItem?.Id;

        return Border(
                FlexColumn(
                    VStack(4,
                        TextBlock(VaultDisplay.FilterTitle(state)).SemiBold(),
                        TextBlock(VaultDisplay.FilterDescription(state)).Foreground(Theme.SecondaryText))
                    .Margin(left: 12, top: 12, right: 12, bottom: 0),
                    AutoSuggestBox(state.SearchQuery, query => Props.Dispatch(new SearchChanged(query)))
                        .PlaceholderText("搜索密码库...")
                        .QueryIcon(SymbolIcon("Find"))
                        .AutomationName("搜索密码库")
                        .Margin(12),
                    items.Count == 0
                        ? RenderEmptyList()
                        : (ListView(items, item => item.Id, (item, _) =>
                            Component<VaultListItem, VaultListItemProps>(
                                    new VaultListItemProps(item, item.Id == selectedId, state.Filter == VaultFilter.Trash, Props.Dispatch))
                                .HorizontalAlignment(HorizontalAlignment.Stretch))
                            with { SelectionMode = ListViewSelectionMode.Single })
                            .SelectionChanged<BitwardenItem>(selected =>
                                Props.Dispatch(new ItemSelected(selected.FirstOrDefault()?.Id)))
                            .Flex(grow: 1, basis: 0),
                    Border(
                            HStack(8,
                                TextBlock($"{items.Count} 个项目").Foreground(Theme.SecondaryText),
                                !string.IsNullOrWhiteSpace(state.SearchQuery)
                                    ? TextBlock($"搜索：{state.SearchQuery}")
                                        .Foreground(Theme.SecondaryText)
                                        .TextTrimming(TextTrimming.CharacterEllipsis)
                                    : null))
                        .Padding(left: 12, top: 8, right: 12, bottom: 8)
                        .Background(Theme.SubtleFill)
                        .Flex(shrink: 0))
                .Flex(grow: 1, basis: 0))
            .WithBorder(Theme.DividerStroke, 1)
            .Flex(grow: 1, basis: 0);
    }

    private Element RenderEmptyList() =>
        Border(
                VStack(10,
                    Icon(FontIcon("\uE8F1", fontSize: 34)),
                    TextBlock(VaultDisplay.EmptyListTitle(Props.State)).SemiBold(),
                    TextBlock(VaultDisplay.EmptyListDescription(Props.State)).Foreground(Theme.SecondaryText)))
            .Flex(grow: 1, basis: 0)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);
}
