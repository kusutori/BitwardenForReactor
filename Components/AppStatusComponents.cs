using System;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Components;

public sealed record AppNoticeProps(AppState State, Action<AppAction> Dispatch);

public sealed class AppNotice : Component<AppNoticeProps>
{
    public override Element Render() =>
        InfoBar(Props.State.NoticeTitle, Props.State.NoticeMessage)
            .Severity(Props.State.NoticeSeverity)
            .IsClosable(true)
            .Closed(() => Props.Dispatch(new NoticeCleared()))
            .Margin(left: 12, top: 8, right: 12, bottom: 0);
}

public sealed class BusyIndicator : Component<AppState>
{
    public override Element Render() =>
        Border(
                HStack(10,
                    ProgressRing().IsActive().Width(22).Height(22),
                    TextBlock(string.IsNullOrWhiteSpace(Props.BusyText) ? "处理中..." : Props.BusyText)
                        .Foreground(Theme.SecondaryText)))
            .Padding(12)
            .Background(Theme.SubtleFill);
}
