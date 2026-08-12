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
            ? T("Connect to Bitwarden CLI")
            : !status.IsLoggedIn
                ? T("Sign-in required")
                : T("Vault locked");
        Element? statusBanner = status is null
            ? InfoBar(T("Bitwarden CLI was not detected"), T("Install Bitwarden CLI or configure the path to bw.exe in Settings."))
                .Severity(InfoBarSeverity.Error)
            : !status.IsLoggedIn
                ? InfoBar(T("Not signed in"), T("Sign in to the current account in this app. Credentials are passed only to this CLI process and are not saved in settings."))
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
                                TextBlock(status?.UserEmail ?? T("Bitwarden vault"))
                                    .Foreground(Theme.SecondaryText)
                                    .HorizontalAlignment(HorizontalAlignment.Center)),
                            statusBanner,
                            status?.IsLoggedIn == false
                                ? VStack(12,
                                    ComboBox([T("Email and master password"), "API Key", "SSO"], loginMode, setLoginMode)
                                        .Width(360)
                                        .Header(T("Sign-in method"))
                                        .AutomationName(T("Sign-in method")),
                                    loginMode == 0
                                        ? VStack(10,
                                            TextBox(email, setEmail, header: T("Email"))
                                                .Width(360)
                                                .AutomationName(T("Sign-in email")),
                                            PasswordBox(Props.MasterPassword, Props.SetMasterPassword, T("Enter master password"))
                                                .Header(T("Master password"))
                                                .Width(360)
                                                .AutomationName(T("Sign-in master password")),
                                            Button(T("Logins"), () => _ = AppCommands.LoginWithPasswordAsync(email, Props.MasterPassword, Props.Dispatch))
                                                .AccentButton()
                                                .Width(360)
                                                .AutomationName(T("Logins"))
                                                .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(Props.MasterPassword)))
                                        : loginMode == 1
                                            ? VStack(10,
                                            TextBox(clientId, setClientId, header: "Client ID")
                                                .Width(360)
                                                .AutomationName("API Client ID"),
                                            PasswordBox(clientSecret, setClientSecret, T("Enter Client Secret"))
                                                .Header("Client Secret")
                                                .Width(360)
                                                .AutomationName("API Client Secret"),
                                            Button(T("Sign in with API key"), () => _ = AppCommands.LoginWithApiKeyAsync(clientId, clientSecret, Props.Dispatch))
                                                .AccentButton()
                                                .Width(360)
                                                .AutomationName(T("Sign in with API key"))
                                                .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret)))
                                            : VStack(10,
                                                TextBlock(T("Your browser will open to complete single sign-on."))
                                                    .Foreground(Theme.SecondaryText),
                                                Button(T("Sign in with SSO"), () => _ = AppCommands.LoginWithSsoAsync(Props.Dispatch))
                                                    .AccentButton()
                                                    .Width(360)
                                                    .AutomationName(T("Sign in with SSO"))
                                                    .IsEnabled(!state.IsBusy)))
                                : VStack(12,
                                PasswordBox(Props.MasterPassword, Props.SetMasterPassword, T("Enter master password"))
                                    .Header(T("Master password"))
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
                                    .AutomationName(T("Master password")),
                                Button(T("Unlock vault"), () => _ = AppCommands.UnlockAsync(Props.MasterPassword, Props.SetMasterPassword, Props.Dispatch))
                                    .AccentButton()
                                    .Width(360)
                                    .Height(40)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(Props.MasterPassword))
                                    .AutomationName(T("Unlock vault"))),
                            Border(VStack())
                                .Height(1)
                                .Background(Theme.DividerStroke),
                            Grid(
                                columns: [GridSize.Auto, GridSize.Auto],
                                rows: [GridSize.Auto],
                                TextBlock(T("Did the CLI status change?"))
                                    .Foreground(Theme.SecondaryText)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Grid(column: 0),
                                HyperlinkButton(T("Check again"), onClick: () => _ = AppCommands.InitializeAsync(Props.Dispatch))
                                    .Padding(0)
                                    .Margin(left: 14)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .AutomationName(T("Check status again"))
                                    .Grid(column: 1))
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
