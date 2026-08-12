using System;
using BitwardenForReactor.Application;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record DeleteConfirmationDialogProps(
    BitwardenItem Target,
    bool Permanent,
    Action<AppAction> Dispatch);

public sealed class DeleteConfirmationDialog : Component<DeleteConfirmationDialogProps>
{
    public override Element Render()
    {
        var message = Props.Permanent
            ? T("Permanently delete “{name}”? This action cannot be undone.", ("name", Props.Target.Name))
            : T("Move “{name}” to Trash?", ("name", Props.Target.Name));

        return ContentDialog(T("Confirm deletion"), TextBlock(message).TextWrapping(), Props.Permanent ? T("Delete permanently") : T("Delete")) with
        {
            IsOpen = true,
            SecondaryButtonText = T("Cancel"),
            CloseButtonText = string.Empty,
            DefaultButton = ContentDialogButton.Secondary,
            OnClosed = result =>
            {
                if (result == ContentDialogResult.Primary)
                {
                    _ = AppCommands.DeleteAsync(Props.Target, Props.Permanent, Props.Dispatch);
                }
                else
                {
                    Props.Dispatch(new DeleteCancelled());
                }
            }
        };
    }
}
