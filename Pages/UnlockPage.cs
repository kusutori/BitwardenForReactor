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
        var title = status is null
            ? "连接 Bitwarden CLI"
            : !status.IsLoggedIn
                ? "需要先登录"
                : "密码库已锁定";
        Element? statusBanner = status is null
            ? InfoBar("未检测到 Bitwarden CLI", "请安装 Bitwarden CLI，或在设置中配置 bw.exe 路径。")
                .Severity(InfoBarSeverity.Error)
            : !status.IsLoggedIn
                ? InfoBar("尚未登录", "请先在终端执行 bw login，然后回到此应用解锁。")
                    .Severity(InfoBarSeverity.Warning)
                : null;

        return Border(
                Border(
                        VStack(20,
                            VStack(10,
                                Border(Icon(FontIcon("\uE72E", fontSize: 30)))
                                    .Width(56)
                                    .Height(56)
                                    .CornerRadius(8)
                                    .Background(Theme.SubtleFill)
                                    .HorizontalAlignment(HorizontalAlignment.Center),
                                Heading(title).HorizontalAlignment(HorizontalAlignment.Center),
                                TextBlock(status?.UserEmail ?? "Bitwarden 密码库")
                                    .Foreground(Theme.SecondaryText)
                                    .HorizontalAlignment(HorizontalAlignment.Center)),
                            statusBanner,
                            VStack(12,
                                PasswordBox(Props.MasterPassword, Props.SetMasterPassword, "输入主密码")
                                    .Header("主密码")
                                    .Width(360)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .OnKeyDown((_, e) =>
                                    {
                                        if (e.Key == VirtualKey.Enter)
                                        {
                                            _ = AppCommands.UnlockAsync(Props.MasterPassword, Props.SetMasterPassword, Props.Dispatch);
                                        }
                                    })
                                    .IsEnabled(!state.IsBusy)
                                    .AutomationName("主密码"),
                                Button("解锁密码库", () => _ = AppCommands.UnlockAsync(Props.MasterPassword, Props.SetMasterPassword, Props.Dispatch))
                                    .AccentButton()
                                    .Width(360)
                                    .Height(40)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(Props.MasterPassword))
                                    .AutomationName("解锁密码库")),
                            Border(VStack())
                                .Height(1)
                                .Background(Theme.DividerStroke),
                            HStack(8,
                                TextBlock("CLI 状态发生变化？").Foreground(Theme.SecondaryText),
                                HyperlinkButton("重新检测", onClick: () => _ = AppCommands.InitializeAsync(Props.Dispatch))
                                    .AutomationName("重新检测状态"))
                            .HorizontalAlignment(HorizontalAlignment.Center)))
                    .Padding(28)
                    .Width(440)
                    .CornerRadius(8)
                    .Background(Theme.CardBackground)
                    .WithBorder(Theme.CardStroke, 1)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center))
            .Padding(24)
            .Flex(grow: 1, basis: 0);
    }
}
