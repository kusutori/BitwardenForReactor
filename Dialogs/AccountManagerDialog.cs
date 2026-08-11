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
                            TextBlock(account.Email ?? account.ServerUrl ?? T("尚未登录"))
                                .Foreground(Theme.SecondaryText)
                                .TextTrimming(TextTrimming.CharacterEllipsis))
                        .Grid(column: 0),
                        HStack(8,
                            account.Id == settings.ActiveAccountId
                                ? Button(T("退出"), () => _ = AppCommands.LogoutActiveAccountAsync(Props.Dispatch))
                                    .AutomationName(T("退出账号 {name}", ("name", account.DisplayName)))
                                : null,
                            Button(T("删除"), () => _ = AppCommands.RemoveAccountAsync(account.Id, Props.Dispatch))
                                .IsEnabled(settings.Accounts.Count > 1)
                                .AutomationName(T("删除账号 {name}", ("name", account.DisplayName))))
                            .Grid(column: 1)))
                .Padding(12)
                .CornerRadius(6)
                .Background(Theme.CardBackground)
                .WithBorder(Theme.CardStroke, 1)
                .WithKey(account.Id.ToString("D")))
            .ToArray();

        return ContentDialog(
            T("管理账号"),
            ScrollView(
                VStack(16,
                    VStack(8,
                        SubHeading(T("现有账号")),
                        VStack(8, accountRows)),
                    VStack(10,
                        SubHeading(T("添加账号")),
                        TextBox(name, setName, header: T("账号名称"))
                            .AutomationName(T("新账号名称")),
                        TextBox(server, setServer, placeholderText: T("留空使用 Bitwarden 云端"), header: T("服务器地址"))
                            .AutomationName(T("新账号服务器地址")),
                        ComboBox([T("主密码"), "API Key", "SSO"], mode, setMode)
                            .Header(T("认证方式"))
                            .AutomationName(T("新账号认证方式")))))
            .Width(520),
            T("添加账号")) with
        {
            IsOpen = true,
            SecondaryButtonText = T("关闭"),
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
