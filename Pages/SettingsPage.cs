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
using static BitwardenForReactor.Controls.Toolkit.ToolkitFactories;
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
                                "深色模式",
                                "使用深色主题显示应用界面。",
                                ToggleSwitch(
                                        settings.IsDarkMode,
                                        value => Change(settings with { IsDarkMode = value }),
                                        onContent: "已开启",
                                        offContent: "已关闭")
                                    .AutomationName("深色模式"),
                                "\uE708")),
                        VStack(8,
                            SubHeading("基础设置"),
                            SettingsCard(
                                "Bitwarden CLI 路径",
                                "默认使用 PATH 中的 bw。需要自定义位置时填写 bw.exe 的完整路径。",
                                TextBox(settings.BwPath, value => Change(settings with { BwPath = value }))
                                    .Width(320)
                                    .AutomationName("Bitwarden CLI 路径"),
                                "\uE756"),
                            SettingsCard(
                                "剪贴板自动清除",
                                "复制敏感字段后，应用会在指定秒数后清空剪贴板。0 表示不自动清除。",
                                TextBox(settings.ClipboardClearSeconds.ToString(CultureInfo.InvariantCulture),
                                        value => Change(settings with
                                        {
                                            ClipboardClearSeconds = VaultDisplay.ParsePositiveInt(value, settings.ClipboardClearSeconds)
                                        }))
                                    .Width(120)
                                    .AutomationName("剪贴板自动清除秒数"),
                                "\uE8C8"),
                            SettingsCard(
                                "自动锁定",
                                "预留设置项。后续可用于空闲超时锁定密码库。",
                                TextBox(settings.AutoLockMinutes.ToString(CultureInfo.InvariantCulture),
                                        value => Change(settings with
                                        {
                                            AutoLockMinutes = VaultDisplay.ParsePositiveInt(value, settings.AutoLockMinutes)
                                        }))
                                    .Width(120)
                                    .AutomationName("自动锁定分钟数"),
                                "\uE72E")),
                        VStack(8,
                            SubHeading("认证与环境"),
                            SettingsExpander(
                                "Bitwarden CLI 环境",
                                "配置 API Key 和自定义环境变量。普通使用场景仍建议先在终端执行 bw login。",
                                TextBlock("高级").Foreground(Theme.SecondaryText),
                                [
                                    SettingsCard(
                                        "BW_CLIENTID",
                                        "用于 Bitwarden CLI API Key 登录的 Client ID。",
                                        TextBox(settings.BwClientId, value => Change(settings with { BwClientId = value }))
                                            .Width(320)
                                            .AutomationName("BW_CLIENTID"),
                                        "\uE77B"),
                                    SettingsCard(
                                        "BW_CLIENTSECRET",
                                        "用于 Bitwarden CLI API Key 登录的 Client Secret。",
                                        PasswordBox(settings.BwClientSecret, value => Change(settings with { BwClientSecret = value }), "输入 Client Secret")
                                            .Width(320)
                                            .AutomationName("BW_CLIENTSECRET"),
                                        "\uE8D7"),
                                    SettingsCard(
                                        "自定义环境变量",
                                        "格式为 KEY1=VALUE1;KEY2=VALUE2，会附加到每次 bw 命令执行环境。",
                                        TextBox(settings.CustomEnvironment, value => Change(settings with { CustomEnvironment = value }))
                                            .PlaceholderText("KEY1=VALUE1;KEY2=VALUE2")
                                            .Width(360)
                                            .AutomationName("自定义环境变量"),
                                        "\uE9D9")
                                ],
                                "\uE713",
                                isExpanded: true)),
                        VStack(8,
                            SubHeading("诊断"),
                            SettingsCard(
                                "当前状态",
                                "显示最近一次 bw status 的结果。",
                                VStack(2,
                                    TextBlock(status?.UserEmail ?? "未检测到账户").TextTrimming(TextTrimming.CharacterEllipsis),
                                    TextBlock(VaultDisplay.FormatStatus(status)).Foreground(Theme.SecondaryText))
                                .Width(280),
                                "\uE946"),
                            SettingsCard(
                                "重新检测",
                                "重新调用 bw status，确认 CLI 路径、登录状态和密码库锁定状态。",
                                Button("检测状态", () => _ = AppCommands.InitializeAsync(Props.Dispatch)).AutomationName("检测状态"),
                                "\uE895")),
                        HStack(8,
                            Button("保存设置", () => _ = AppCommands.SaveSettingsAsync(state.Settings, Props.Dispatch))
                                .Background(Theme.Accent)
                                .AutomationName("保存设置"),
                            Button("放弃更改", () => Change(SettingsManager.Instance.Current))
                                .AutomationName("放弃设置更改"))))
                .Padding(24)
                .MaxWidth(720))
            .Flex(grow: 1, basis: 0);
    }

    private void Change(AppSettings settings) => Props.Dispatch(new SettingsChanged(settings));
}
