using System;
using BitwardenForReactor.Application;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Pages;

public sealed record UnlockPageProps(
    AppState State,
    Action<AppAction> Dispatch,
    string MasterPassword,
    Action<string> SetMasterPassword);

public sealed class UnlockPage : Component<UnlockPageProps>
{
    public override Element Render()
    {
        var state = Props.State;
        var status = state.Status;
        Element statusBanner = status is null
            ? InfoBar("未检测到 Bitwarden CLI", "请安装 Bitwarden CLI，或在设置中配置 bw.exe 路径。")
                .Severity(InfoBarSeverity.Error)
            : !status.IsLoggedIn
                ? InfoBar("尚未登录", "请先在终端执行 bw login，然后回到此应用解锁。")
                    .Severity(InfoBarSeverity.Warning)
                : TextBlock(status.UserEmail ?? "已登录").Foreground(Theme.SecondaryText);

        return Border(
                FlexColumn(
                    Icon(FontIcon("\uE72E", fontSize: 58)),
                    Heading("Bitwarden"),
                    statusBanner,
                    PasswordBox(Props.MasterPassword, Props.SetMasterPassword, "输入主密码")
                        .Header("主密码")
                        .OnKeyDown((_, e) =>
                        {
                            if (e.Key == VirtualKey.Enter)
                            {
                                _ = AppCommands.UnlockAsync(Props.MasterPassword, Props.SetMasterPassword, Props.Dispatch);
                            }
                        })
                        .IsEnabled(!state.IsBusy)
                        .AutomationName("主密码"),
                    Button("解锁", () => _ = AppCommands.UnlockAsync(Props.MasterPassword, Props.SetMasterPassword, Props.Dispatch))
                        .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(Props.MasterPassword))
                        .Background(Theme.Accent)
                        .AutomationName("解锁密码库"),
                    HyperlinkButton("重新检测状态", onClick: () => _ = AppCommands.InitializeAsync(Props.Dispatch))
                        .AutomationName("重新检测状态"))
                with { RowGap = 16, AlignItems = FlexAlign.Center })
            .Padding(32)
            .MaxWidth(440)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Flex(grow: 1, basis: 0);
    }
}
