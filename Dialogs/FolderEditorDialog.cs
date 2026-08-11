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
                            Heading(isEditing ? T("编辑文件夹") : T("新建文件夹"))
                                .Margin(left: 20, top: 18, right: 20, bottom: 10)
                                .Grid(row: 0),
                            VStack(10,
                                TextBlock(isEditing ? T("修改名称后会覆盖当前文件夹名称。") : T("输入名称后会在当前密码库中创建文件夹。"))
                                    .Foreground(Theme.SecondaryText)
                                    .TextWrapping(),
                                TextBox(name, value =>
                                    {
                                        setName(value);
                                        setConfirmDelete(false);
                                    }, header: T("文件夹名称（必填）"))
                                    .AutomationName(T("文件夹名称"))
                                    .OnKeyDown((_, e) =>
                                    {
                                        if (e.Key == VirtualKey.Enter)
                                        {
                                            Save();
                                            e.Handled = true;
                                        }
                                    }),
                                TextBlock(T("通过在父文件夹名后面添加“/”来嵌套文件夹。示例：Social/Forums"))
                                    .Foreground(Theme.SecondaryText)
                                    .TextWrapping())
                                .Padding(left: 20, top: 4, right: 20, bottom: 18)
                                .Grid(row: 1),
                            Border(
                                    Grid(
                                        columns: [GridSize.Star(), GridSize.Auto],
                                        rows: [GridSize.Auto],
                                        HStack(12,
                                                Button(isEditing ? T("保存") : T("创建"), Save)
                                                    .AccentButton()
                                                    .MinWidth(96)
                                                    .IsEnabled(!string.IsNullOrWhiteSpace(name))
                                                    .AutomationName(isEditing ? T("保存文件夹") : T("创建文件夹")),
                                                Button(T("取消"), () => Props.Dispatch(new FolderEditorClosed()))
                                                    .MinWidth(96)
                                                    .AutomationName(T("取消编辑文件夹")))
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
                    .AutomationName(T("文件夹编辑器")))
            .Background(Theme.SmokeFill)
            .AutomationName(T("文件夹编辑器遮罩"));
    }

    private Element DeleteButton(BitwardenFolder folder, bool confirmDelete, Action<bool> setConfirmDelete) =>
        Button(
            confirmDelete
                ? TextBlock(T("确认删除"))
                : Icon(FontIcon("\uE74D", fontSize: 18)),
            () =>
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
            .AutomationName(confirmDelete ? T("确认删除文件夹") : T("删除文件夹"))
            .ToolTip(confirmDelete ? T("确认删除文件夹") : T("删除文件夹"));
}
