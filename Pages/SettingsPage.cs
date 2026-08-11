using System;
using System.Globalization;
using BitwardenForReactor.Application;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using static BitwardenForReactor.Controls.Toolkit.SettingsCardElement;
using static BitwardenForReactor.Controls.Toolkit.SettingsExpanderElement;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Pages;

public sealed record SettingsPageProps(AppState State, Action<AppAction> Dispatch);

public sealed class SettingsPage : Component<SettingsPageProps>
{
    public override Element Render()
    {
        var state = Props.State;
        var settings = state.Settings;
        var status = state.Status;

        return ScrollView(
                Border(
                    VStack(18,
                        Heading("设置"),
                        TextBlock("配置 Bitwarden CLI、剪贴板安全策略和认证环境。设置只在点击保存后写入本地文件。")
                            .Foreground(Theme.SecondaryText)
                            .TextWrapping(),
                        VStack(8,
                            SubHeading("外观"),
                            SettingsCard(
                                header: "应用主题",
                                description: "选择应用界面的颜色模式。",
                                content: ComboBox(
                                        ["跟随系统", "浅色", "深色"],
                                        Math.Clamp((int)settings.ThemeMode, 0, 2),
                                        index => Change(settings with
                                        {
                                            ThemeMode = (AppThemeMode)Math.Clamp(index, 0, 2)
                                        }))
                                    .Width(160)
                                    .AutomationName("应用主题"),
                                headerIcon: Icon(FontIcon("\uE708")))),
                        VStack(8,
                            SubHeading("基础设置"),
                            SettingsCard(
                                header: "Bitwarden CLI 路径",
                                description: "默认使用 PATH 中的 bw。需要自定义位置时填写 bw.exe 的完整路径。",
                                content: TextBox(settings.Cli.ExecutablePath, value => Change(settings with { Cli = settings.Cli with { ExecutablePath = value } }))
                                    .Width(320)
                                    .AutomationName("Bitwarden CLI 路径"),
                                headerIcon: Icon(FontIcon("\uE756"))),
                            SettingsCard(
                                header: "剪贴板自动清除",
                                description: "复制敏感字段后，应用会在指定秒数后清空剪贴板。0 表示不自动清除。",
                                content: TextBox(settings.ClipboardClearSeconds.ToString(CultureInfo.InvariantCulture),
                                        value => Change(settings with
                                        {
                                            ClipboardClearSeconds = VaultDisplay.ParsePositiveInt(value, settings.ClipboardClearSeconds)
                                        }))
                                    .Width(120)
                                    .AutomationName("剪贴板自动清除秒数"),
                                headerIcon: Icon(FontIcon("\uE8C8"))),
                            SettingsCard(
                                header: "自动锁定",
                                description: "预留设置项。后续可用于空闲超时锁定密码库。",
                                content: TextBox(settings.AutoLockMinutes.ToString(CultureInfo.InvariantCulture),
                                        value => Change(settings with
                                        {
                                            AutoLockMinutes = VaultDisplay.ParsePositiveInt(value, settings.AutoLockMinutes)
                                        }))
                                    .Width(120)
                                    .AutomationName("自动锁定分钟数"),
                                headerIcon: Icon(FontIcon("\uE72E")))),
                        VStack(8,
                            SubHeading("认证与环境"),
                            SettingsExpander(
                                header: "Bitwarden CLI 环境",
                                description: "配置附加到每次 CLI 调用的非敏感环境变量。账号凭据只在登录时临时使用。",
                                content: TextBlock("高级").Foreground(Theme.SecondaryText),
                                items: new object[]
                                {
                                    SettingsCard(
                                        header: "自定义环境变量",
                                        description: "格式为 KEY1=VALUE1;KEY2=VALUE2。请勿在此保存密码、Session 或 API Secret。",
                                        content: TextBox(settings.Cli.CustomEnvironment, value => Change(settings with { Cli = settings.Cli with { CustomEnvironment = value } }))
                                            .PlaceholderText("KEY1=VALUE1;KEY2=VALUE2")
                                            .Width(360)
                                            .AutomationName("自定义环境变量"),
                                        headerIcon: Icon(FontIcon("\uE9D9")))
                                },
                                headerIcon: Icon(FontIcon("\uE713")),
                                isExpanded: true)),
                        VStack(8,
                            SubHeading("诊断"),
                            SettingsCard(
                                header: "当前状态",
                                description: "显示最近一次 bw status 的结果。",
                                content: VStack(2,
                                    TextBlock(status?.UserEmail ?? "未检测到账户").TextTrimming(TextTrimming.CharacterEllipsis),
                                    TextBlock(VaultDisplay.FormatStatus(status)).Foreground(Theme.SecondaryText))
                                .Width(280),
                                headerIcon: Icon(FontIcon("\uE946"))),
                            SettingsCard(
                                header: "重新检测",
                                description: "重新调用 bw status，确认 CLI 路径、登录状态和密码库锁定状态。",
                                content: Button("检测状态", () => _ = AppCommands.InitializeAsync(Props.Dispatch)).AutomationName("检测状态"),
                                headerIcon: Icon(FontIcon("\uE895")))),
                        HStack(8,
                            Button("保存设置", () => _ = AppCommands.SaveSettingsAsync(state.Settings, Props.Dispatch))
                                .AccentButton()
                                .AutomationName("保存设置"),
                            Button("放弃更改", () => Change(SettingsManager.Instance.Current))
                                .AutomationName("放弃设置更改"))))
                .Padding(24)
                .MaxWidth(720))
            .Flex(grow: 1, basis: 0);
    }

    private void Change(AppSettings settings) => Props.Dispatch(new SettingsChanged(settings));
}
