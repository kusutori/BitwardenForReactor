using System;
using BitwardenForReactor.Application;
using BitwardenForReactor.Dialogs;
using BitwardenForReactor.Localization;
using BitwardenForReactor.Services;
using BitwardenForReactor.Shell;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Localization;
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

        var locale = AppLocales.FromLanguage(state.Settings.Language);

        return LocaleProvider(
            locale,
            Component<LocalizedApp, LocalizedAppProps>(
                    new LocalizedAppProps(state, dispatch, masterPassword, setMasterPassword))
                .WithKey(locale),
            resourceProvider: AppResourceProvider.Instance,
            defaultLocale: AppLocales.English);
    }
}

public sealed record LocalizedAppProps(
    AppState State,
    Action<AppAction> Dispatch,
    string MasterPassword,
    Action<string> SetMasterPassword);

public sealed class LocalizedApp : Component<LocalizedAppProps>
{
    public override Element Render()
    {
        var t = UseIntl();
        AppText.Use(t);
        var state = Props.State;

        return Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Star()],
                Component<BitwardenShell, BitwardenShellProps>(
                    new BitwardenShellProps(state, Props.Dispatch, Props.MasterPassword, Props.SetMasterPassword)),
                state.EditorDraft is { } draft
                    ? Component<ItemEditorDialog, ItemEditorDialogProps>(new ItemEditorDialogProps(draft, state.Folders, Props.Dispatch))
                    : null,
                state.ShowFolderEditor
                    ? Component<FolderEditorDialog, FolderEditorDialogProps>(new FolderEditorDialogProps(state.FolderEditorTarget, Props.Dispatch))
                        .WithKey(state.FolderEditorTarget?.Id ?? "new-folder")
                    : null,
                state.ShowGenerator
                    ? Component<GeneratorDialog, GeneratorDialogProps>(new GeneratorDialogProps(state, Props.Dispatch))
                    : null,
                state.ImportExportDialog == ImportExportDialogKind.Import
                    ? Component<ImportDialog, ImportDialogProps>(new ImportDialogProps(state, Props.Dispatch))
                    : null,
                state.ImportExportDialog == ImportExportDialogKind.Export
                    ? Component<ExportDialog, ExportDialogProps>(new ExportDialogProps(state, Props.Dispatch))
                    : null,
                state.DeleteTarget is { } target
                    ? Component<DeleteConfirmationDialog, DeleteConfirmationDialogProps>(
                        new DeleteConfirmationDialogProps(target, state.DeletePermanently, Props.Dispatch))
                    : null,
                state.ShowAccountManager
                    ? Component<AccountManagerDialog, AccountManagerDialogProps>(
                        new AccountManagerDialogProps(state, Props.Dispatch))
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
