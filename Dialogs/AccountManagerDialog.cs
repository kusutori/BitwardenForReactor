using System;
using System.Linq;
using BitwardenForReactor.Application;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record AccountManagerDialogProps(AppState State, Action<AppAction> Dispatch);

public sealed class AccountManagerDialog : Component<AccountManagerDialogProps>
{
    public override Element Render()
    {
        var (name, setName) = UseState(string.Empty);
        var (server, setServer) = UseState(string.Empty);
        var (mode, setMode) = UseState(0);
        var settings = Props.State.Settings;

        var accountRows = settings.Accounts.Select(account =>
            Border(
                    Grid(columns: [GridSize.Star(), GridSize.Auto], rows: [GridSize.Auto],
                        VStack(2,
                            TextBlock(account.DisplayName).SemiBold(),
                            TextBlock(account.Email ?? account.ServerUrl ?? T("Not signed in"))
                                .Foreground(Theme.SecondaryText)
                                .TextTrimming(TextTrimming.CharacterEllipsis))
                        .Grid(column: 0),
                        HStack(8,
                            account.Id == settings.ActiveAccountId
                                ? Button(T("Sign out"), () => _ = AppCommands.LogoutActiveAccountAsync(Props.Dispatch))
                                    .AutomationName(T("Sign out of {name}", ("name", account.DisplayName)))
                                : null,
                            Button(T("Delete"), () => _ = AppCommands.RemoveAccountAsync(account.Id, Props.Dispatch))
                                .IsEnabled(settings.Accounts.Count > 1)
                                .AutomationName(T("Delete account {name}", ("name", account.DisplayName))))
                            .Grid(column: 1)))
                .Padding(12)
                .CornerRadius(6)
                .Background(Theme.CardBackground)
                .WithBorder(Theme.CardStroke, 1)
                .WithKey(account.Id.ToString("D")))
            .ToArray();

        return ContentDialog(
            T("Manage accounts"),
            ScrollView(
                VStack(16,
                    VStack(8,
                        SubHeading(T("Existing accounts")),
                        VStack(8, accountRows)),
                    VStack(10,
                        SubHeading(T("Add account")),
                        TextBox(name, setName, header: T("Account name"))
                            .AutomationName(T("New account name")),
                        TextBox(server, setServer, placeholderText: T("Leave blank to use Bitwarden cloud"), header: T("Server URL"))
                            .AutomationName(T("New account server URL")),
                        ComboBox([T("Master password"), "API Key", "SSO"], mode, setMode)
                            .Header(T("Authentication method"))
                            .AutomationName(T("New account authentication method")))))
            .Width(520),
            T("Add account")) with
        {
            IsOpen = true,
            SecondaryButtonText = T("Close"),
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(name),
            DefaultButton = ContentDialogButton.Primary,
            OnClosed = result =>
            {
                if (result == ContentDialogResult.Primary)
                {
                    _ = AppCommands.AddAccountAsync(
                        name,
                        server,
                        (AccountAuthenticationMode)Math.Clamp(mode, 0, 2),
                        Props.Dispatch);
                }
                else
                {
                    Props.Dispatch(new AccountManagerVisibilityChanged(false));
                }
            }
        };
    }
}
