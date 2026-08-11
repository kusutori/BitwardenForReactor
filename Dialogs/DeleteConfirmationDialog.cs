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
            ? T("确定要永久删除「{name}」吗？此操作无法撤销。", ("name", Props.Target.Name))
            : T("确定要将「{name}」移入回收站吗？", ("name", Props.Target.Name));

        return ContentDialog(T("确认删除"), TextBlock(message).TextWrapping(), Props.Permanent ? T("永久删除") : T("删除")) with
        {
            IsOpen = true,
            SecondaryButtonText = T("取消"),
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
