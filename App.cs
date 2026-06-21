using BitwardenForReactor.Application;
using BitwardenForReactor.Dialogs;
using BitwardenForReactor.Services;
using BitwardenForReactor.Shell;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor;

public sealed class App : Component
{
    public override Element Render()
    {
        var (state, dispatch) = UseReducer<AppState, AppAction>(AppReducer.Reduce, new AppState());
        var (masterPassword, setMasterPassword) = UseState(string.Empty);

        UseEffect(() => { _ = AppCommands.InitializeAsync(dispatch); });

        return FlexColumn(
                Component<BitwardenShell, BitwardenShellProps>(
                        new BitwardenShellProps(state, dispatch, masterPassword, setMasterPassword))
                    .Flex(grow: 1, basis: 0),
                state.EditorDraft is { } draft
                    ? Component<ItemEditorDialog, ItemEditorDialogProps>(new ItemEditorDialogProps(draft, dispatch))
                    : null,
                state.DeleteTarget is { } target
                    ? Component<DeleteConfirmationDialog, DeleteConfirmationDialogProps>(
                        new DeleteConfirmationDialogProps(target, state.DeletePermanently, dispatch))
                    : null)
            .Backdrop(BackdropKind.Mica)
            .RequestedTheme(state.Settings.ThemeMode switch
            {
                AppThemeMode.Light => ElementTheme.Light,
                AppThemeMode.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            });
    }
}
