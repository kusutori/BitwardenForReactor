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
            TextBlock(T("目的地")).SemiBold(),
            Card(VStack(14,
                ComboBox([T("我的密码库")], 0, _ => { })
                    .Header(T("密码库"))
                    .IsEnabled(false)
                    .AutomationName(T("导入目标密码库")),
                ComboBox([T("不指定文件夹")], 0, _ => { })
                    .Header(T("文件夹"))
                    .IsEnabled(false)
                    .AutomationName(T("导入目标文件夹")),
                TextBlock(T("Bitwarden CLI 当前不支持在导入时指定目标文件夹。需要移动到文件夹时，请在导入后批量整理。"))
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())),
            TextBlock(T("数据")).SemiBold(),
            Card(VStack(14,
                ComboBox(formats.ToArray(), formatIndex, setFormatIndex)
                    .Header(T("文件格式"))
                    .AutomationName(T("导入文件格式")),
                VStack(6,
                    TextBlock(T("选择要导入的文件")).SemiBold(),
                    Grid(
                        columns: [GridSize.Star(), GridSize.Auto],
                        rows: [GridSize.Auto],
                        TextBox(filePath, setFilePath, placeholderText: T("未选择文件"))
                            .AutomationName(T("导入文件路径"))
                            .Grid(column: 0),
                        Button(T("选择文件"), PickImportFile)
                            .MinWidth(96)
                            .AutomationName(T("选择导入文件"))
                            .Grid(column: 1)
                            .Margin(left: 8))),
                TextBox(pastedContent, setPastedContent, header: T("或复制/粘贴要导入的文件内容"))
                    .AcceptsReturn()
                    .TextWrapping()
                    .MinHeight(112)
                    .AutomationName(T("导入文件内容")),
                TextBlock(T("如果同时选择了文件并粘贴了内容，将优先导入所选文件。"))
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())))
            .Padding(24);

        return DialogShell(
            T("导入"),
            body,
            HStack(12,
                Button(T("导入"), () => _ = AppCommands.ImportVaultAsync(selectedFormat, filePath, pastedContent, Props.Dispatch))
                    .AccentButton()
                    .MinWidth(96)
                    .IsEnabled(canImport)
                    .AutomationName(T("导入密码库")),
                Button(T("取消"), () => Props.Dispatch(new ImportExportVisibilityChanged(null)))
                    .MinWidth(96)
                    .AutomationName(T("取消导入"))));
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
            .AutomationName(T("{title}遮罩", ("title", title)));

    private static Element Card(Element child) =>
        Border(child)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(8)
            .Padding(24)
            .HorizontalAlignment(HorizontalAlignment.Stretch);
}
