using System;
using BitwardenForReactor.Components;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Pages;

public sealed record VaultPageProps(AppState State, Action<AppAction> Dispatch);

public sealed class VaultPage : Component<VaultPageProps>
{
    public override Element Render() =>
        FlexRow(
                Component<VaultListPane, VaultPaneProps>(new VaultPaneProps(Props.State, Props.Dispatch))
                    .Flex(shrink: 0, basis: 390),
                Component<ItemDetailPane, VaultPaneProps>(new VaultPaneProps(Props.State, Props.Dispatch))
                    .Flex(grow: 1, basis: 0))
            .Flex(grow: 1, basis: 0);
}
