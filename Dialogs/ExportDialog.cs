using System;
using BitwardenCli.Core.ImportExport;
using BitwardenForReactor.Application;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record ExportDialogProps(AppState State, Action<AppAction> Dispatch);

public sealed class ExportDialog : Component<ExportDialogProps>
{
    private static readonly string[] FormatNames =
    [
        "csv",
        "json",
        "json (Encrypted)",
        "zip (with attachments)"
    ];

    private static readonly VaultExportFormat[] FormatValues =
    [
        VaultExportFormat.Csv,
        VaultExportFormat.Json,
        VaultExportFormat.EncryptedJson,
        VaultExportFormat.Zip
    ];

    public override Element Render()
    {
        var (formatIndex, setFormatIndex) = UseState(0);
        var (outputPath, setOutputPath) = UseState(string.Empty);

        async void PickOutputPath()
        {
            var format = FormatValues[Math.Clamp(formatIndex, 0, FormatValues.Length - 1)];
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "bitwarden-export"
            };
            picker.FileTypeChoices.Add(FormatNames[formatIndex], [ExtensionFor(format)]);
            DialogPicker.Initialize(picker);

            var file = await picker.PickSaveFileAsync();
            if (file is not null)
            {
                setOutputPath(file.Path);
            }
        }

        var selectedFormat = FormatValues[Math.Clamp(formatIndex, 0, FormatValues.Length - 1)];
        var canExport = !Props.State.IsBusy && !string.IsNullOrWhiteSpace(outputPath);
        var body = VStack(18,
            TextBlock(T("Export settings")).SemiBold(),
            Card(VStack(14,
                ComboBox(FormatNames, formatIndex, index =>
                    {
                        setFormatIndex(index);
                        setOutputPath(string.Empty);
                    })
                    .Header(T("Export file type"))
                    .AutomationName(T("Export file type")),
                VStack(6,
                    TextBlock(T("Export file")).SemiBold(),
                    Grid(
                        columns: [GridSize.Star(), GridSize.Auto],
                        rows: [GridSize.Auto],
                        TextBox(outputPath, setOutputPath, placeholderText: T("No file selected"))
                            .AutomationName(T("Export file path"))
                            .Grid(column: 0),
                        Button(T("Choose location"), PickOutputPath)
                            .MinWidth(96)
                            .AutomationName(T("Choose export location"))
                            .Grid(column: 1)
                            .Margin(left: 8))),
                TextBlock(T("The exported file may contain sensitive data. Save it in a trusted location."))
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())))
            .Padding(24);

        return DialogShell(
            T("Export"),
            body,
            HStack(12,
                Button(T("Export"), () => _ = AppCommands.ExportVaultAsync(selectedFormat, outputPath, Props.Dispatch))
                    .AccentButton()
                    .MinWidth(96)
                    .IsEnabled(canExport)
                    .AutomationName(T("Export vault")),
                Button(T("Cancel"), () => Props.Dispatch(new ImportExportVisibilityChanged(null)))
                    .MinWidth(96)
                    .AutomationName(T("Cancel export"))));
    }

    private static string ExtensionFor(VaultExportFormat format) => format switch
    {
        VaultExportFormat.Csv => ".csv",
        VaultExportFormat.Json => ".json",
        VaultExportFormat.EncryptedJson => ".json",
        VaultExportFormat.Zip => ".zip",
        _ => ".json"
    };

    private static Element DialogShell(string title, Element body, Element footer) =>
        Border(
                Border(
                        Grid(
                            columns: [GridSize.Star()],
                            rows: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                            Heading(title)
                                .Margin(left: 24, top: 20, right: 24, bottom: 12)
                                .Grid(row: 0),
                            ScrollView(body)
                                .HorizontalScrollMode(ScrollingScrollMode.Disabled)
                                .Grid(row: 1),
                            Border(footer.HorizontalAlignment(HorizontalAlignment.Left))
                                .WithBorder(Theme.CardStroke, 1)
                                .Padding(16)
                                .Grid(row: 2)))
                    .Background(Theme.SolidBackground)
                    .WithBorder(Theme.CardStroke, 1)
                    .CornerRadius(8)
                    .MinWidth(420)
                    .MaxWidth(560)
                    .MaxHeight(520)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .VerticalAlignment(VerticalAlignment.Stretch)
                    .AutomationName(title)
                    .Margin(24)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center))
            .Background(Theme.SmokeFill)
            .AutomationName(T("{title} overlay", ("title", title)));

    private static Element Card(Element child) =>
        Border(child)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(8)
            .Padding(24)
            .HorizontalAlignment(HorizontalAlignment.Stretch);
}
