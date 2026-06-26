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
            TextBlock("导出设置").SemiBold(),
            Card(VStack(14,
                ComboBox(FormatNames, formatIndex, index =>
                    {
                        setFormatIndex(index);
                        setOutputPath(string.Empty);
                    })
                    .Header("导出文件类型")
                    .AutomationName("导出文件类型"),
                VStack(6,
                    TextBlock("导出文件").SemiBold(),
                    Grid(
                        columns: [GridSize.Star(), GridSize.Auto],
                        rows: [GridSize.Auto],
                        TextBox(outputPath, setOutputPath, placeholderText: "未选择文件")
                            .AutomationName("导出文件路径")
                            .Grid(column: 0),
                        Button("选择位置", PickOutputPath)
                            .MinWidth(96)
                            .AutomationName("选择导出位置")
                            .Grid(column: 1)
                            .Margin(left: 8))),
                TextBlock("导出的文件可能包含敏感数据，请保存到可信位置。")
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())))
            .Padding(24);

        return DialogShell(
            "导出",
            body,
            HStack(12,
                Button("导出", () => _ = AppCommands.ExportVaultAsync(selectedFormat, outputPath, Props.Dispatch))
                    .AccentButton()
                    .MinWidth(96)
                    .IsEnabled(canExport)
                    .AutomationName("导出密码库"),
                Button("取消", () => Props.Dispatch(new ImportExportVisibilityChanged(null)))
                    .MinWidth(96)
                    .AutomationName("取消导出")));
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
            .AutomationName($"{title}遮罩");

    private static Element Card(Element child) =>
        Border(child)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(8)
            .Padding(24)
            .HorizontalAlignment(HorizontalAlignment.Stretch);
}
