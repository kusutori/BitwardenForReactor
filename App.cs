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

        return Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Star()],
                Component<BitwardenShell, BitwardenShellProps>(
                    new BitwardenShellProps(state, dispatch, masterPassword, setMasterPassword)),
                state.EditorDraft is { } draft
                    ? Component<ItemEditorDialog, ItemEditorDialogProps>(new ItemEditorDialogProps(draft, state.Folders, dispatch))
                    : null,
                state.ShowFolderEditor
                    ? Component<FolderEditorDialog, FolderEditorDialogProps>(new FolderEditorDialogProps(dispatch))
                    : null,
                state.DeleteTarget is { } target
                    ? Component<DeleteConfirmationDialog, DeleteConfirmationDialogProps>(
                        new DeleteConfirmationDialogProps(target, state.DeletePermanently, dispatch))
                    : null,
                state.ShowAccountManager
                    ? Component<AccountManagerDialog, AccountManagerDialogProps>(
                        new AccountManagerDialogProps(state, dispatch))
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
