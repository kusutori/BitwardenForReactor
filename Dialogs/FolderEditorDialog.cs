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
                            Heading(isEditing ? T("Edit folder") : T("New folder"))
                                .Margin(left: 20, top: 18, right: 20, bottom: 10)
                                .Grid(row: 0),
                            VStack(10,
                                TextBlock(isEditing ? T("Changing the name will replace the current folder name.") : T("Enter a name to create a folder in the current vault."))
                                    .Foreground(Theme.SecondaryText)
                                    .TextWrapping(),
                                TextBox(name, value =>
                                    {
                                        setName(value);
                                        setConfirmDelete(false);
                                    }, header: T("Folder name (required)"))
                                    .AutomationName(T("Folder name"))
                                    .OnKeyDown((_, e) =>
                                    {
                                        if (e.Key == VirtualKey.Enter)
                                        {
                                            Save();
                                            e.Handled = true;
                                        }
                                    }),
                                TextBlock(T("To nest folders, add “/” after the parent folder name. Example: Social/Forums"))
                                    .Foreground(Theme.SecondaryText)
                                    .TextWrapping())
                                .Padding(left: 20, top: 4, right: 20, bottom: 18)
                                .Grid(row: 1),
                            Border(
                                    Grid(
                                        columns: [GridSize.Star(), GridSize.Auto],
                                        rows: [GridSize.Auto],
                                        HStack(12,
                                                Button(isEditing ? T("Save") : T("Create"), Save)
                                                    .AccentButton()
                                                    .MinWidth(96)
                                                    .IsEnabled(!string.IsNullOrWhiteSpace(name))
                                                    .AutomationName(isEditing ? T("Save folder") : T("Create folder")),
                                                Button(T("Cancel"), () => Props.Dispatch(new FolderEditorClosed()))
                                                    .MinWidth(96)
                                                    .AutomationName(T("Cancel folder editing")))
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
                    .AutomationName(T("Folder editor")))
            .Background(Theme.SmokeFill)
            .AutomationName(T("Folder editor overlay"));
    }

    private Element DeleteButton(BitwardenFolder folder, bool confirmDelete, Action<bool> setConfirmDelete) =>
        Button(
            confirmDelete
                ? TextBlock(T("Confirm deletion"))
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
            .AutomationName(confirmDelete ? T("Confirm folder deletion") : T("Delete folder"))
            .ToolTip(confirmDelete ? T("Confirm folder deletion") : T("Delete folder"));
}
