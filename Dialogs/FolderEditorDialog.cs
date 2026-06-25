using System;
using BitwardenForReactor.Application;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record FolderEditorDialogProps(BitwardenFolder? Folder, Action<AppAction> Dispatch);

public sealed class FolderEditorDialog : Component<FolderEditorDialogProps>
{
    public override Element Render()
    {
        var (name, setName) = UseState(Props.Folder?.Name ?? string.Empty);
        var (confirmDelete, setConfirmDelete) = UseState(false);
        var isEditing = Props.Folder is not null;

        void Save()
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _ = AppCommands.SaveFolderAsync(Props.Folder, name, Props.Dispatch);
            }
        }

        return Border(
                Border(
                        Grid(
                            columns: [GridSize.Star()],
                            rows: [GridSize.Auto, GridSize.Auto, GridSize.Auto],
                            Heading(isEditing ? "编辑文件夹" : "新建文件夹")
                                .Margin(left: 20, top: 18, right: 20, bottom: 10)
                                .Grid(row: 0),
                            VStack(10,
                                TextBlock(isEditing ? "修改名称后会覆盖当前文件夹名称。" : "输入名称后会在当前密码库中创建文件夹。")
                                    .Foreground(Theme.SecondaryText)
                                    .TextWrapping(),
                                TextBox(name, value =>
                                    {
                                        setName(value);
                                        setConfirmDelete(false);
                                    }, header: "文件夹名称（必填）")
                                    .AutomationName("文件夹名称")
                                    .OnKeyDown((_, e) =>
                                    {
                                        if (e.Key == VirtualKey.Enter)
                                        {
                                            Save();
                                            e.Handled = true;
                                        }
                                    }),
                                TextBlock("通过在父文件夹名后面添加“/”来嵌套文件夹。示例：Social/Forums")
                                    .Foreground(Theme.SecondaryText)
                                    .TextWrapping())
                                .Padding(left: 20, top: 4, right: 20, bottom: 18)
                                .Grid(row: 1),
                            Border(
                                    Grid(
                                        columns: [GridSize.Star(), GridSize.Auto],
                                        rows: [GridSize.Auto],
                                        HStack(12,
                                                Button(isEditing ? "保存" : "创建", Save)
                                                    .AccentButton()
                                                    .MinWidth(96)
                                                    .IsEnabled(!string.IsNullOrWhiteSpace(name))
                                                    .AutomationName(isEditing ? "保存文件夹" : "创建文件夹"),
                                                Button("取消", () => Props.Dispatch(new FolderEditorClosed()))
                                                    .MinWidth(96)
                                                    .AutomationName("取消编辑文件夹"))
                                            .Grid(column: 0),
                                        isEditing
                                            ? DeleteButton(Props.Folder!, confirmDelete, setConfirmDelete)
                                                .Grid(column: 1)
                                            : null))
                                .WithBorder(Theme.CardStroke, 1)
                                .Padding(16)
                                .Grid(row: 2)))
                    .Background(Theme.SolidBackground)
                    .WithBorder(Theme.CardStroke, 1)
                    .CornerRadius(8)
                    .Width(420)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .AutomationName("文件夹编辑器"))
            .Background(Theme.SmokeFill)
            .AutomationName("文件夹编辑器遮罩");
    }

    private Element DeleteButton(BitwardenFolder folder, bool confirmDelete, Action<bool> setConfirmDelete) =>
        Button(confirmDelete ? "确认删除" : string.Empty, () =>
            {
                if (confirmDelete)
                {
                    _ = AppCommands.DeleteFolderAsync(folder, Props.Dispatch);
                    return;
                }

                setConfirmDelete(true);
            })
            .MinWidth(confirmDelete ? 96 : 40)
            .Foreground(Theme.SystemCritical)
            .AutomationName(confirmDelete ? "确认删除文件夹" : "删除文件夹")
            .Set(button =>
            {
                if (confirmDelete)
                {
                    button.Content = "确认删除";
                    return;
                }

                button.Content = new FontIcon
                {
                    Glyph = "\uE74D",
                    FontSize = 18
                };
                ToolTipService.SetToolTip(button, "删除文件夹");
            });
}
