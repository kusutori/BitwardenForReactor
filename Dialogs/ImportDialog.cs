using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Application;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record ImportDialogProps(AppState State, Action<AppAction> Dispatch);

public sealed class ImportDialog : Component<ImportDialogProps>
{
    private static readonly string[] FallbackImportFormats =
    [
        "bitwardenjson",
        "bitwardencsv",
        "chromejson",
        "chromecsv",
        "firefoxcsv",
        "keepass2xml",
        "lastpasscsv",
        "1password1pux",
        "1passwordcsv",
        "dashlanecsv",
        "nordpasscsv",
        "protonpasscsv"
    ];

    public override Element Render()
    {
        var (formats, setFormats) = UseState<IReadOnlyList<string>>(FallbackImportFormats);
        var (formatIndex, setFormatIndex) = UseState(0);
        var (filePath, setFilePath) = UseState(string.Empty);
        var (pastedContent, setPastedContent) = UseState(string.Empty);

        UseEffect(() =>
        {
            async void LoadFormats()
            {
                try
                {
                    var cliFormats = await BitwardenApplicationService.Instance.GetImportFormatsAsync();
                    if (cliFormats.Count > 0)
                    {
                        setFormats(cliFormats);
                        setFormatIndex(0);
                    }
                }
                catch
                {
                    setFormats(FallbackImportFormats);
                }
            }

            LoadFormats();
        });

        async void PickImportFile()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            DialogPicker.Initialize(picker);

            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                setFilePath(file.Path);
            }
        }

        var selectedFormat = formats[Math.Clamp(formatIndex, 0, formats.Count - 1)];
        var canImport = !Props.State.IsBusy &&
            !string.IsNullOrWhiteSpace(selectedFormat) &&
            (!string.IsNullOrWhiteSpace(filePath) || !string.IsNullOrWhiteSpace(pastedContent));

        var body = VStack(18,
            TextBlock(T("Destination")).SemiBold(),
            Card(VStack(14,
                ComboBox([T("My vault")], 0, _ => { })
                    .Header(T("Vault"))
                    .IsEnabled(false)
                    .AutomationName(T("Destination vault")),
                ComboBox([T("Do not assign a folder")], 0, _ => { })
                    .Header(T("Folders"))
                    .IsEnabled(false)
                    .AutomationName(T("Destination folder")),
                TextBlock(T("Bitwarden CLI does not currently support selecting a destination folder during import. Organize imported items afterward if needed."))
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())),
            TextBlock(T("Data")).SemiBold(),
            Card(VStack(14,
                ComboBox(formats.ToArray(), formatIndex, setFormatIndex)
                    .Header(T("File format"))
                    .AutomationName(T("Import file format")),
                VStack(6,
                    TextBlock(T("Select a file to import")).SemiBold(),
                    Grid(
                        columns: [GridSize.Star(), GridSize.Auto],
                        rows: [GridSize.Auto],
                        TextBox(filePath, setFilePath, placeholderText: T("No file selected"))
                            .AutomationName(T("Import file path"))
                            .Grid(column: 0),
                        Button(T("Choose file"), PickImportFile)
                            .MinWidth(96)
                            .AutomationName(T("Choose import file"))
                            .Grid(column: 1)
                            .Margin(left: 8))),
                TextBox(pastedContent, setPastedContent, header: T("Or paste the contents of the file to import"))
                    .AcceptsReturn()
                    .TextWrapping()
                    .MinHeight(112)
                    .AutomationName(T("Import file contents")),
                TextBlock(T("If you select a file and paste content, the selected file takes precedence."))
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())))
            .Padding(24);

        return DialogShell(
            T("Import"),
            body,
            HStack(12,
                Button(T("Import"), () => _ = AppCommands.ImportVaultAsync(selectedFormat, filePath, pastedContent, Props.Dispatch))
                    .AccentButton()
                    .MinWidth(96)
                    .IsEnabled(canImport)
                    .AutomationName(T("Import vault")),
                Button(T("Cancel"), () => Props.Dispatch(new ImportExportVisibilityChanged(null)))
                    .MinWidth(96)
                    .AutomationName(T("Cancel import"))));
    }

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
                    .MaxWidth(640)
                    .MaxHeight(720)
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
