using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using BitwardenCli.Core.ImportExport;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace BitwardenForReactor.Application;

public static class AppCommands
{
    private static CancellationTokenSource _accountOperations = new();

    public static async Task InitializeAsync(Action<AppAction> dispatch, CancellationToken cancellationToken = default)
    {
        dispatch(new BusyChanged(true, T("Checking Bitwarden status...")));
        try
        {
            var status = await BitwardenApplicationService.Instance.GetStatusAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            dispatch(new StatusLoaded(status));
            if (status is not null)
            {
                await UpdateActiveAccountMetadataAsync(status, dispatch);
            }
            if (status?.IsUnlocked == true)
            {
                await LoadVaultAsync(dispatch, cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested) dispatch(new BusyChanged(false));
        }
    }

    public static async Task UnlockAsync(string masterPassword, Action<string> setMasterPassword, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(masterPassword))
        {
            dispatch(new NoticeShown(T("Master password required"), T("Enter your master password."), InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, T("Unlocking...")));
        try
        {
            var result = await BitwardenApplicationService.Instance.UnlockAsync(masterPassword);
            if (!result.Success)
            {
                dispatch(new NoticeShown(T("Unlock failed"), result.Message, InfoBarSeverity.Error));
                setMasterPassword(string.Empty);
                return;
            }

            setMasterPassword(string.Empty);
            dispatch(new NoticeShown(T("Unlocked"), result.Message, InfoBarSeverity.Success));
            dispatch(new StatusLoaded(await BitwardenApplicationService.Instance.GetStatusAsync()));
            await LoadVaultAsync(dispatch);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task LoadVaultAsync(Action<AppAction> dispatch, string? selectedItemId = null, CancellationToken cancellationToken = default)
    {
        dispatch(new BusyChanged(true, T("Loading vault...")));
        try
        {
            var service = BitwardenApplicationService.Instance;
            var itemsTask = service.GetItemsResultAsync(cancellationToken);
            var trashTask = service.GetTrashItemsResultAsync(cancellationToken);
            var archivedTask = service.GetArchivedItemsResultAsync(cancellationToken);
            var foldersTask = service.GetFoldersResultAsync(cancellationToken);
            await Task.WhenAll(itemsTask, trashTask, archivedTask, foldersTask);
            cancellationToken.ThrowIfCancellationRequested();
            var itemsResult = await itemsTask;
            var trashResult = await trashTask;
            var archivedResult = await archivedTask;
            var foldersResult = await foldersTask;
            if (!itemsResult.IsSuccess)
            {
                dispatch(new NoticeShown(
                    T("Vault loading failed"),
                    BitwardenApplicationService.DescribeError(itemsResult.Error, T("Could not parse the item data returned by Bitwarden CLI.")),
                    InfoBarSeverity.Error));
                return;
            }

            var items = itemsResult.Value ?? [];
            var trash = trashResult.Value ?? [];
            var archived = archivedResult.Value ?? [];
            var folders = foldersResult.Value ?? [];
            if (!trashResult.IsSuccess || !archivedResult.IsSuccess || !foldersResult.IsSuccess)
            {
                dispatch(new NoticeShown(
                    T("Some data could not be loaded"),
                    T("Regular items were loaded, but trash, archive, or folders are temporarily unavailable."),
                    InfoBarSeverity.Warning));
            }
            dispatch(new VaultLoaded(items, trash, archived, folders, selectedItemId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested) dispatch(new BusyChanged(false));
        }
    }

    public static async Task SyncAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Synchronizing...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.SyncAsync();
            dispatch(success
                ? new NoticeShown(T("Sync complete"), T("Vault synchronized."), InfoBarSeverity.Success)
                : new NoticeShown(T("Sync failed"), T("Make sure the vault is unlocked and the network is available."), InfoBarSeverity.Error));
            if (success)
            {
                await LoadVaultAsync(dispatch);
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task LockAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Locking...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.LockAsync();
            if (success)
            {
                dispatch(new Locked());
                dispatch(new NoticeShown(T("Locked"), T("Vault locked."), InfoBarSeverity.Success));
            }
            else
            {
                dispatch(new NoticeShown(T("Lock failed"), T("Bitwarden CLI could not lock the vault."), InfoBarSeverity.Error));
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task SaveDraftAsync(VaultItemDraft draft, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, draft.Id is null ? T("Creating item...") : T("Saving item...")));
        try
        {
            var service = BitwardenApplicationService.Instance;
            var success = draft.Id is null
                ? await service.CreateItemAsync(draft.ToJsonObject())
                : await service.EditItemAsync(draft.Id, draft.ToJsonObject());

            if (!success)
            {
                dispatch(new NoticeShown(T("Save failed"), T("Bitwarden CLI could not save the item."), InfoBarSeverity.Error));
                return;
            }

            dispatch(new EditorClosed());
            dispatch(new NoticeShown(T("Saved"), T("Item saved."), InfoBarSeverity.Success));
            await LoadVaultAsync(dispatch, draft.Id);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task SaveFolderAsync(BitwardenFolder? existingFolder, string name, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            dispatch(new NoticeShown(existingFolder is null ? T("Could not create folder") : T("Could not save folder"), T("Enter a folder name."), InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, existingFolder is null ? T("Creating folder...") : T("Saving folder...")));
        try
        {
            var service = BitwardenApplicationService.Instance;
            var folder = existingFolder is null
                ? await service.CreateFolderAsync(name.Trim())
                : await service.EditFolderAsync(existingFolder.Id, name.Trim());
            if (folder is null)
            {
                dispatch(new NoticeShown(existingFolder is null ? T("Creation failed") : T("Save failed"), T("Bitwarden CLI could not save the folder."), InfoBarSeverity.Error));
                return;
            }

            dispatch(new FolderEditorClosed());
            dispatch(new NoticeShown(existingFolder is null ? T("Created") : T("Saved"), T("Folder “{name}” saved.", ("name", folder.Name)), InfoBarSeverity.Success));
            await LoadVaultAsync(dispatch);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task DeleteFolderAsync(BitwardenFolder folder, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Deleting folder...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.DeleteFolderAsync(folder.Id);
            if (!success)
            {
                dispatch(new NoticeShown(T("Deletion failed"), T("Bitwarden CLI could not delete the folder."), InfoBarSeverity.Error));
                return;
            }

            dispatch(new FolderEditorClosed());
            dispatch(new NoticeShown(T("Deleted"), T("Folder “{name}” deleted.", ("name", folder.Name)), InfoBarSeverity.Success));
            await LoadVaultAsync(dispatch);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task DeleteAsync(BitwardenItem item, bool permanent, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, permanent ? T("Deleting permanently...") : T("Deleting...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.DeleteItemAsync(item.Id, permanent);
            dispatch(new DeleteCancelled());
            if (!success)
            {
                dispatch(new NoticeShown(T("Deletion failed"), T("Bitwarden CLI could not delete the item."), InfoBarSeverity.Error));
                return;
            }

            dispatch(new NoticeShown(T("Deleted"), permanent ? T("Item permanently deleted.") : T("Item moved to Trash."), InfoBarSeverity.Success));
            await LoadVaultAsync(dispatch);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task RestoreAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Restoring...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.RestoreItemAsync(item.Id);
            dispatch(success
                ? new NoticeShown(T("Restored"), T("Item restored."), InfoBarSeverity.Success)
                : new NoticeShown(T("Restore failed"), T("Bitwarden CLI could not restore the item."), InfoBarSeverity.Error));
            if (success)
            {
                await LoadVaultAsync(dispatch, item.Id);
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task ArchiveAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Archiving...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.ArchiveItemAsync(item.Id);
            dispatch(success
                ? new NoticeShown(T("Archived"), T("Item archived."), InfoBarSeverity.Success)
                : new NoticeShown(T("Archive failed"), T("Bitwarden CLI could not archive the item."), InfoBarSeverity.Error));
            if (success)
            {
                await LoadVaultAsync(dispatch);
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task CopyTotpAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        var totp = await BitwardenApplicationService.Instance.GetTotpAsync(item.Id);
        if (string.IsNullOrWhiteSpace(totp))
        {
            dispatch(new NoticeShown(T("TOTP unavailable"), T("Could not retrieve the verification code."), InfoBarSeverity.Warning));
            return;
        }

        await CopyAsync(totp, dispatch);
    }

    public static async Task CopyAsync(string value, Action<AppAction> dispatch)
    {
        await ClipboardService.CopyToClipboardWithTimeoutAsync(value, SettingsManager.Instance.Current.ClipboardClearSeconds);
        dispatch(new NoticeShown(T("Copied"), T("Copied to the clipboard. It will be cleared according to your settings."), InfoBarSeverity.Success));
    }

    public static async Task ImportVaultAsync(string format, string? filePath, string pastedContent, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            dispatch(new NoticeShown(T("Could not import"), T("Select a file format."), InfoBarSeverity.Warning));
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath) && string.IsNullOrWhiteSpace(pastedContent))
        {
            dispatch(new NoticeShown(T("Could not import"), T("Select a file to import or paste its contents."), InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, T("Importing...")));
        try
        {
            var service = BitwardenApplicationService.Instance;
            var success = !string.IsNullOrWhiteSpace(filePath)
                ? await service.ImportVaultFromFileAsync(format, filePath)
                : await service.ImportVaultFromContentAsync(format, pastedContent, GuessImportExtension(format));

            if (!success)
            {
                dispatch(new NoticeShown(T("Import failed"), T("Bitwarden CLI could not import the file. Make sure the format matches its contents."), InfoBarSeverity.Error));
                return;
            }

            dispatch(new ImportExportVisibilityChanged(null));
            dispatch(new NoticeShown(T("Import complete"), T("Vault data was imported."), InfoBarSeverity.Success));
            await LoadVaultAsync(dispatch);
        }
        catch
        {
            dispatch(new NoticeShown(T("Import failed"), T("The import options are invalid, or Bitwarden CLI could not read the file."), InfoBarSeverity.Error));
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task ExportVaultAsync(VaultExportFormat format, string outputPath, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            dispatch(new NoticeShown(T("Could not export"), T("Choose where to save the exported file."), InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, T("Exporting...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.ExportVaultAsync(format, outputPath);
            if (!success)
            {
                dispatch(new NoticeShown(T("Export failed"), T("Bitwarden CLI could not export the vault."), InfoBarSeverity.Error));
                return;
            }

            dispatch(new ImportExportVisibilityChanged(null));
            dispatch(new NoticeShown(T("Export complete"), T("Vault exported to {path}", ("path", outputPath)), InfoBarSeverity.Success));
        }
        catch
        {
            dispatch(new NoticeShown(T("Export failed"), T("The export path is invalid, or Bitwarden CLI could not write the file."), InfoBarSeverity.Error));
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task OpenUriAsync(string? uriText, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(uriText))
        {
            dispatch(new NoticeShown(T("Could not open website"), T("This item does not have a website URL."), InfoBarSeverity.Warning));
            return;
        }

        var normalized = uriText.Contains("://", StringComparison.Ordinal) ? uriText : $"https://{uriText}";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || !await Launcher.LaunchUriAsync(uri))
        {
            dispatch(new NoticeShown(T("Could not open website"), T("Windows could not open the website URL for this item."), InfoBarSeverity.Error));
        }
    }

    private static string GuessImportExtension(string format)
    {
        if (format.Contains("json", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("1pux", StringComparison.OrdinalIgnoreCase))
        {
            return ".json";
        }

        if (format.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return ".xml";
        }

        if (format.Contains("csv", StringComparison.OrdinalIgnoreCase))
        {
            return ".csv";
        }

        return ".txt";
    }

    public static async Task ToggleFavoriteAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, item.Favorite ? T("Removing from favorites...") : T("Adding to favorites...")));
        try
        {
            var update = new JsonObject { ["favorite"] = !item.Favorite };
            var success = await BitwardenApplicationService.Instance.EditItemAsync(item.Id, update);
            dispatch(success
                ? new NoticeShown(item.Favorite ? T("Removed from favorites") : T("Added to favorites"), item.Name, InfoBarSeverity.Success)
                : new NoticeShown(T("Operation failed"), T("Bitwarden CLI could not update the favorite status."), InfoBarSeverity.Error));
            if (success)
            {
                await LoadVaultAsync(dispatch, item.Id);
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task CloneItemAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Cloning item...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.CloneItemAsync(item.Id, T("{name} copy", ("name", item.Name)));
            dispatch(success
                ? new NoticeShown(T("Cloned"), T("Created “{name} copy”.", ("name", item.Name)), InfoBarSeverity.Success)
                : new NoticeShown(T("Clone failed"), T("Bitwarden CLI could not clone the item."), InfoBarSeverity.Error));
            if (success)
            {
                await LoadVaultAsync(dispatch);
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task SaveSettingsAsync(AppSettings settings, Action<AppAction> dispatch)
    {
        await SettingsManager.Instance.SaveAsync(settings);
        BitwardenApplicationService.Instance.Reconfigure(settings);
        dispatch(new SettingsSaved(settings));
        dispatch(new NoticeShown(T("Settings saved"), T("The new settings are now active."), InfoBarSeverity.Success));
    }

    public static async Task SwitchAccountAsync(Guid accountId, Action<AppAction> dispatch)
    {
        var current = SettingsManager.Instance.Current;
        if (accountId == current.ActiveAccountId) return;
        CancelAccountOperations();
        var settings = current with { ActiveAccountId = accountId };
        await SettingsManager.Instance.SaveAsync(settings);
        BitwardenApplicationService.Instance.Reconfigure(settings);
        BitwardenApplicationService.Instance.SwitchAccount(accountId);
        dispatch(new AccountSwitched(settings));
        await InitializeAsync(dispatch, _accountOperations.Token);
    }

    public static async Task AddAccountAsync(
        string displayName,
        string? serverUrl,
        AccountAuthenticationMode authenticationMode,
        Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            dispatch(new NoticeShown(T("Could not add account"), T("Enter an account name."), InfoBarSeverity.Warning));
            return;
        }

        var id = Guid.NewGuid();
        var account = new AccountSettings
        {
            Id = id,
            DisplayName = displayName.Trim(),
            ServerUrl = string.IsNullOrWhiteSpace(serverUrl) ? null : serverUrl.Trim(),
            AuthenticationMode = authenticationMode,
            CliDataDirectory = Path.Combine(SettingsManager.GetAccountsRoot(), id.ToString("D"), "cli")
        };
        var current = SettingsManager.Instance.Current;
        var settings = current with { Accounts = [.. current.Accounts, account], ActiveAccountId = id };
        await SettingsManager.Instance.SaveAsync(settings);
        BitwardenApplicationService.Instance.Reconfigure(settings);
        dispatch(new AccountSwitched(settings));
        await InitializeAsync(dispatch, _accountOperations.Token);
    }

    public static async Task RemoveAccountAsync(Guid accountId, Action<AppAction> dispatch)
    {
        var current = SettingsManager.Instance.Current;
        if (current.Accounts.Count <= 1)
        {
            dispatch(new NoticeShown(T("Could not delete account"), T("At least one account is required."), InfoBarSeverity.Warning));
            return;
        }

        var accounts = current.Accounts.Where(account => account.Id != accountId).ToArray();
        var activeId = current.ActiveAccountId == accountId ? accounts[0].Id : current.ActiveAccountId;
        var settings = current with { Accounts = accounts, ActiveAccountId = activeId };
        CancelAccountOperations();
        await SettingsManager.Instance.SaveAsync(settings);
        BitwardenApplicationService.Instance.Reconfigure(settings);
        dispatch(new AccountSwitched(settings));
        await InitializeAsync(dispatch, _accountOperations.Token);
    }

    public static async Task LoginWithPasswordAsync(string email, string password, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            dispatch(new NoticeShown(T("Could not sign in"), T("Enter your email and master password."), InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, T("Signing in...")));
        try
        {
            var result = await BitwardenApplicationService.Instance.LoginWithPasswordAsync(email, password);
            dispatch(new NoticeShown(result.Success ? T("Signed in") : T("Sign-in failed"), result.Message, result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
            if (result.Success) await InitializeAsync(dispatch);
        }
        finally { dispatch(new BusyChanged(false)); }
    }

    public static async Task LoginWithApiKeyAsync(string clientId, string clientSecret, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            dispatch(new NoticeShown(T("Could not sign in"), T("Enter the Client ID and Client Secret."), InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, T("Signing in with API key...")));
        try
        {
            var result = await BitwardenApplicationService.Instance.LoginWithApiKeyAsync(clientId, clientSecret);
            dispatch(new NoticeShown(result.Success ? T("Signed in") : T("Sign-in failed"), result.Message, result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
            if (result.Success) await InitializeAsync(dispatch);
        }
        finally { dispatch(new BusyChanged(false)); }
    }

    public static async Task LoginWithSsoAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Opening SSO sign-in...")));
        try
        {
            var result = await BitwardenApplicationService.Instance.LoginWithSsoAsync();
            dispatch(new NoticeShown(result.Success ? T("Signed in") : T("Sign-in failed"), result.Message, result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
            if (result.Success) await InitializeAsync(dispatch);
        }
        finally { dispatch(new BusyChanged(false)); }
    }

    public static async Task LogoutActiveAccountAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, T("Signing out...")));
        try
        {
            var success = await BitwardenApplicationService.Instance.LogoutAsync();
            dispatch(new NoticeShown(success ? T("Signed out") : T("Sign-out failed"), success ? T("The CLI session for the current account has been cleared.") : T("Bitwarden CLI could not sign out of the current account."), success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
            if (success)
            {
                dispatch(new Locked());
                await InitializeAsync(dispatch);
            }
        }
        finally { dispatch(new BusyChanged(false)); }
    }

    private static async Task UpdateActiveAccountMetadataAsync(BitwardenStatus status, Action<AppAction> dispatch)
    {
        var current = SettingsManager.Instance.Current;
        var accounts = current.Accounts.Select(account => account.Id == current.ActiveAccountId
            ? account with
            {
                Email = status.UserEmail ?? account.Email,
                UserId = status.UserId ?? account.UserId,
                ServerUrl = status.ServerUrl ?? account.ServerUrl,
                LastUsedAt = DateTimeOffset.Now
            }
            : account).ToArray();
        var settings = current with { Accounts = accounts };
        await SettingsManager.Instance.SaveAsync(settings);
        dispatch(new AccountsChanged(settings));
    }

    private static void CancelAccountOperations()
    {
        _accountOperations.Cancel();
        _accountOperations.Dispose();
        _accountOperations = new CancellationTokenSource();
    }
}
