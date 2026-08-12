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
                        Heading(T("Settings")),
                        TextBlock(T("Configure Bitwarden CLI, clipboard protection, and the authentication environment. Changes are written locally only after you select Save settings."))
                            .Foreground(Theme.SecondaryText)
                            .TextWrapping(),
                        VStack(8,
                            SubHeading(T("Appearance")),
                            SettingsCard(
                                header: T("App language"),
                                description: T("Choose the language used by the app."),
                                content: ComboBox(
                                        [T("Simplified Chinese"), T("English")],
                                        Math.Clamp((int)settings.Language, 0, 1),
                                        index => Change(settings with
                                        {
                                            Language = (AppLanguage)Math.Clamp(index, 0, 1)
                                        }))
                                    .Width(160)
                                    .AutomationName(T("App language")),
                                headerIcon: Icon(FontIcon("\uE8C1"))),
                            SettingsCard(
                                header: T("App theme"),
                                description: T("Choose the app color mode."),
                                content: ComboBox(
                                        [T("Use system setting"), T("Light"), T("Dark")],
                                        Math.Clamp((int)settings.ThemeMode, 0, 2),
                                        index => Change(settings with
                                        {
                                            ThemeMode = (AppThemeMode)Math.Clamp(index, 0, 2)
                                        }))
                                    .Width(160)
                                    .AutomationName(T("App theme")),
                                headerIcon: Icon(FontIcon("\uE708")))),
                        VStack(8,
                            SubHeading(T("General")),
                            SettingsCard(
                                header: T("Bitwarden CLI path"),
                                description: T("Uses bw from PATH by default. Enter the full path to bw.exe to use a custom location."),
                                content: TextBox(settings.Cli.ExecutablePath, value => Change(settings with { Cli = settings.Cli with { ExecutablePath = value } }))
                                    .Width(320)
                                    .AutomationName(T("Bitwarden CLI path")),
                                headerIcon: Icon(FontIcon("\uE756"))),
                            SettingsCard(
                                header: T("Clear clipboard automatically"),
                                description: T("After a sensitive field is copied, the clipboard is cleared after the specified number of seconds. Use 0 to disable automatic clearing."),
                                content: TextBox(settings.ClipboardClearSeconds.ToString(CultureInfo.InvariantCulture),
                                        value => Change(settings with
                                        {
                                            ClipboardClearSeconds = VaultDisplay.ParsePositiveInt(value, settings.ClipboardClearSeconds)
                                        }))
                                    .Width(120)
                                    .AutomationName(T("Clipboard clearing delay in seconds")),
                                headerIcon: Icon(FontIcon("\uE8C8"))),
                            SettingsCard(
                                header: T("Lock automatically"),
                                description: T("Reserved for a future idle-timeout vault lock."),
                                content: TextBox(settings.AutoLockMinutes.ToString(CultureInfo.InvariantCulture),
                                        value => Change(settings with
                                        {
                                            AutoLockMinutes = VaultDisplay.ParsePositiveInt(value, settings.AutoLockMinutes)
                                        }))
                                    .Width(120)
                                    .AutomationName(T("Automatic lock delay in minutes")),
                                headerIcon: Icon(FontIcon("\uE72E")))),
                        VStack(8,
                            SubHeading(T("Authentication and environment")),
                            SettingsExpander(
                                header: T("Bitwarden CLI environment"),
                                description: T("Configure non-sensitive environment variables added to each CLI call. Account credentials are used temporarily only while signing in."),
                                content: TextBlock(T("Advanced")).Foreground(Theme.SecondaryText),
                                items: new object[]
                                {
                                    SettingsCard(
                                        header: T("Custom environment variables"),
                                        description: T("Use KEY1=VALUE1;KEY2=VALUE2 format. Do not store passwords, sessions, or API secrets here."),
                                        content: TextBox(settings.Cli.CustomEnvironment, value => Change(settings with { Cli = settings.Cli with { CustomEnvironment = value } }))
                                            .PlaceholderText("KEY1=VALUE1;KEY2=VALUE2")
                                            .Width(360)
                                            .AutomationName(T("Custom environment variables")),
                                        headerIcon: Icon(FontIcon("\uE9D9")))
                                },
                                headerIcon: Icon(FontIcon("\uE713")),
                                isExpanded: true)),
                        VStack(8,
                            SubHeading(T("Diagnostics")),
                            SettingsCard(
                                header: T("Current status"),
                                description: T("Shows the result of the most recent bw status command."),
                                content: VStack(2,
                                    TextBlock(status?.UserEmail ?? T("No account detected")).TextTrimming(TextTrimming.CharacterEllipsis),
                                    TextBlock(VaultDisplay.FormatStatus(status)).Foreground(Theme.SecondaryText))
                                .Width(280),
                                headerIcon: Icon(FontIcon("\uE946"))),
                            SettingsCard(
                                header: T("Check again"),
                                description: T("Run bw status again to verify the CLI path, sign-in state, and vault lock state."),
                                content: Button(T("Check status"), () => _ = AppCommands.InitializeAsync(Props.Dispatch)).AutomationName(T("Check status")),
                                headerIcon: Icon(FontIcon("\uE895")))),
                        HStack(8,
                            Button(T("Save settings"), () => _ = AppCommands.SaveSettingsAsync(state.Settings, Props.Dispatch))
                                .AccentButton()
                                .AutomationName(T("Save settings")),
                            Button(T("Discard changes"), () => Change(SettingsManager.Instance.Current))
                                .AutomationName(T("Discard settings changes")))))
                .Padding(24)
                .MaxWidth(720))
            .Flex(grow: 1, basis: 0);
    }

    private void Change(AppSettings settings) => Props.Dispatch(new SettingsChanged(settings));
}
