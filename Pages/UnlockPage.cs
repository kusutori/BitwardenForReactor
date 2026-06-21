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
using System.Linq;

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
        var activeAccount = Props.State.Settings.Accounts.First(account => account.Id == Props.State.Settings.ActiveAccountId);
        var (email, setEmail) = UseState(activeAccount.Email ?? string.Empty);
        var (clientId, setClientId) = UseState(string.Empty);
        var (clientSecret, setClientSecret) = UseState(string.Empty);
        var (loginMode, setLoginMode) = UseState((int)activeAccount.AuthenticationMode);
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
                ? InfoBar("尚未登录", "请在此应用登录当前账号。凭据只传给本次 CLI 进程，不会保存到设置。")
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
                            status?.IsLoggedIn == false
                                ? VStack(12,
                                    ComboBox(["邮箱和主密码", "API Key", "SSO"], loginMode, setLoginMode)
                                        .Width(360)
                                        .Header("登录方式")
                                        .AutomationName("登录方式"),
                                    loginMode == 0
                                        ? VStack(10,
                                            TextBox(email, setEmail, header: "邮箱")
                                                .Width(360)
                                                .AutomationName("登录邮箱"),
                                            PasswordBox(Props.MasterPassword, Props.SetMasterPassword, "输入主密码")
                                                .Header("主密码")
                                                .Width(360)
                                                .AutomationName("登录主密码"),
                                            Button("登录", () => _ = AppCommands.LoginWithPasswordAsync(email, Props.MasterPassword, Props.Dispatch))
                                                .AccentButton()
                                                .Width(360)
                                                .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(Props.MasterPassword)))
                                        : loginMode == 1
                                            ? VStack(10,
                                            TextBox(clientId, setClientId, header: "Client ID")
                                                .Width(360)
                                                .AutomationName("API Client ID"),
                                            PasswordBox(clientSecret, setClientSecret, "输入 Client Secret")
                                                .Header("Client Secret")
                                                .Width(360)
                                                .AutomationName("API Client Secret"),
                                            Button("使用 API Key 登录", () => _ = AppCommands.LoginWithApiKeyAsync(clientId, clientSecret, Props.Dispatch))
                                                .AccentButton()
                                                .Width(360)
                                                .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret)))
                                            : VStack(10,
                                                TextBlock("将打开浏览器完成单点登录。")
                                                    .Foreground(Theme.SecondaryText),
                                                Button("使用 SSO 登录", () => _ = AppCommands.LoginWithSsoAsync(Props.Dispatch))
                                                    .AccentButton()
                                                    .Width(360)
                                                    .IsEnabled(!state.IsBusy)))
                                : VStack(12,
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
