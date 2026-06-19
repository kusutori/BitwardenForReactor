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
            ? $"确定要永久删除「{Props.Target.Name}」吗？此操作无法撤销。"
            : $"确定要将「{Props.Target.Name}」移入回收站吗？";

        return ContentDialog("确认删除", TextBlock(message).TextWrapping(), Props.Permanent ? "永久删除" : "删除") with
        {
            IsOpen = true,
            SecondaryButtonText = "取消",
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
