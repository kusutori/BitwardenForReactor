using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace BitwardenForReactor.Application;

public static class AppCommands
{
    private static CancellationTokenSource _accountOperations = new();

    public static async Task InitializeAsync(Action<AppAction> dispatch, CancellationToken cancellationToken = default)
    {
        dispatch(new BusyChanged(true, "检测 Bitwarden 状态..."));
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
            dispatch(new NoticeShown("需要主密码", "请输入主密码。", InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, "正在解锁..."));
        try
        {
            var result = await BitwardenApplicationService.Instance.UnlockAsync(masterPassword);
            if (!result.Success)
            {
                dispatch(new NoticeShown("解锁失败", result.Message, InfoBarSeverity.Error));
                setMasterPassword(string.Empty);
                return;
            }

            setMasterPassword(string.Empty);
            dispatch(new NoticeShown("已解锁", result.Message, InfoBarSeverity.Success));
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
        dispatch(new BusyChanged(true, "正在加载密码库..."));
        try
        {
            var service = BitwardenApplicationService.Instance;
            var itemsTask = service.GetItemsAsync(cancellationToken);
            var trashTask = service.GetTrashItemsAsync(cancellationToken);
            var foldersTask = service.GetFoldersAsync(cancellationToken);
            await Task.WhenAll(itemsTask, trashTask, foldersTask);
            cancellationToken.ThrowIfCancellationRequested();
            var items = await itemsTask ?? [];
            var trash = await trashTask ?? [];
            var folders = await foldersTask ?? [];
            dispatch(new VaultLoaded(items, trash, folders, selectedItemId));
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
        dispatch(new BusyChanged(true, "正在同步..."));
        try
        {
            var success = await BitwardenApplicationService.Instance.SyncAsync();
            dispatch(success
                ? new NoticeShown("同步完成", "密码库已同步。", InfoBarSeverity.Success)
                : new NoticeShown("同步失败", "请确认密码库已解锁且网络可用。", InfoBarSeverity.Error));
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
        dispatch(new BusyChanged(true, "正在锁定..."));
        try
        {
            var success = await BitwardenApplicationService.Instance.LockAsync();
            if (success)
            {
                dispatch(new Locked());
                dispatch(new NoticeShown("已锁定", "密码库已锁定。", InfoBarSeverity.Success));
            }
            else
            {
                dispatch(new NoticeShown("锁定失败", "Bitwarden CLI 未能锁定密码库。", InfoBarSeverity.Error));
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task SaveDraftAsync(VaultItemDraft draft, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, draft.Id is null ? "正在创建项目..." : "正在保存项目..."));
        try
        {
            var service = BitwardenApplicationService.Instance;
            var success = draft.Id is null
                ? await service.CreateItemAsync(draft.ToJsonObject())
                : await service.EditItemAsync(draft.Id, draft.ToJsonObject());

            if (!success)
            {
                dispatch(new NoticeShown("保存失败", "Bitwarden CLI 未能保存该项目。", InfoBarSeverity.Error));
                return;
            }

            dispatch(new EditorClosed());
            dispatch(new NoticeShown("已保存", "项目已保存。", InfoBarSeverity.Success));
            await LoadVaultAsync(dispatch, draft.Id);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task DeleteAsync(BitwardenItem item, bool permanent, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, permanent ? "正在永久删除..." : "正在删除..."));
        try
        {
            var success = await BitwardenApplicationService.Instance.DeleteItemAsync(item.Id, permanent);
            dispatch(new DeleteCancelled());
            if (!success)
            {
                dispatch(new NoticeShown("删除失败", "Bitwarden CLI 未能删除该项目。", InfoBarSeverity.Error));
                return;
            }

            dispatch(new NoticeShown("已删除", permanent ? "项目已永久删除。" : "项目已移入回收站。", InfoBarSeverity.Success));
            await LoadVaultAsync(dispatch);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task RestoreAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, "正在恢复..."));
        try
        {
            var success = await BitwardenApplicationService.Instance.RestoreItemAsync(item.Id);
            dispatch(success
                ? new NoticeShown("已恢复", "项目已恢复。", InfoBarSeverity.Success)
                : new NoticeShown("恢复失败", "Bitwarden CLI 未能恢复该项目。", InfoBarSeverity.Error));
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

    public static async Task CopyTotpAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        var totp = await BitwardenApplicationService.Instance.GetTotpAsync(item.Id);
        if (string.IsNullOrWhiteSpace(totp))
        {
            dispatch(new NoticeShown("TOTP 不可用", "未能获取验证码。", InfoBarSeverity.Warning));
            return;
        }

        await CopyAsync(totp, dispatch);
    }

    public static async Task CopyAsync(string value, Action<AppAction> dispatch)
    {
        await ClipboardService.CopyToClipboardWithTimeoutAsync(value, SettingsManager.Instance.Current.ClipboardClearSeconds);
        dispatch(new NoticeShown("已复制", "内容已复制到剪贴板，并会按设置自动清除。", InfoBarSeverity.Success));
    }

    public static async Task OpenUriAsync(string? uriText, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(uriText))
        {
            dispatch(new NoticeShown("无法打开网站", "该项目没有配置网站地址。", InfoBarSeverity.Warning));
            return;
        }

        var normalized = uriText.Contains("://", StringComparison.Ordinal) ? uriText : $"https://{uriText}";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || !await Launcher.LaunchUriAsync(uri))
        {
            dispatch(new NoticeShown("无法打开网站", "系统未能打开该项目的网站地址。", InfoBarSeverity.Error));
        }
    }

    public static async Task ToggleFavoriteAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, item.Favorite ? "正在取消收藏..." : "正在收藏..."));
        try
        {
            var update = new JsonObject { ["favorite"] = !item.Favorite };
            var success = await BitwardenApplicationService.Instance.EditItemAsync(item.Id, update);
            dispatch(success
                ? new NoticeShown(item.Favorite ? "已取消收藏" : "已收藏", item.Name, InfoBarSeverity.Success)
                : new NoticeShown("操作失败", "Bitwarden CLI 未能更新收藏状态。", InfoBarSeverity.Error));
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
        dispatch(new BusyChanged(true, "正在克隆项目..."));
        try
        {
            var success = await BitwardenApplicationService.Instance.CloneItemAsync(item.Id, $"{item.Name} 副本");
            dispatch(success
                ? new NoticeShown("已克隆", $"已创建「{item.Name} 副本」。", InfoBarSeverity.Success)
                : new NoticeShown("克隆失败", "Bitwarden CLI 未能克隆该项目。", InfoBarSeverity.Error));
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
        dispatch(new NoticeShown("设置已保存", "新设置已生效。", InfoBarSeverity.Success));
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
            dispatch(new NoticeShown("无法添加账号", "请输入账号名称。", InfoBarSeverity.Warning));
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
            dispatch(new NoticeShown("无法删除账号", "至少需要保留一个账号。", InfoBarSeverity.Warning));
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
            dispatch(new NoticeShown("无法登录", "请输入邮箱和主密码。", InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, "正在登录..."));
        try
        {
            var result = await BitwardenApplicationService.Instance.LoginWithPasswordAsync(email, password);
            dispatch(new NoticeShown(result.Success ? "登录成功" : "登录失败", result.Message, result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
            if (result.Success) await InitializeAsync(dispatch);
        }
        finally { dispatch(new BusyChanged(false)); }
    }

    public static async Task LoginWithApiKeyAsync(string clientId, string clientSecret, Action<AppAction> dispatch)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            dispatch(new NoticeShown("无法登录", "请输入 Client ID 和 Client Secret。", InfoBarSeverity.Warning));
            return;
        }

        dispatch(new BusyChanged(true, "正在使用 API Key 登录..."));
        try
        {
            var result = await BitwardenApplicationService.Instance.LoginWithApiKeyAsync(clientId, clientSecret);
            dispatch(new NoticeShown(result.Success ? "登录成功" : "登录失败", result.Message, result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
            if (result.Success) await InitializeAsync(dispatch);
        }
        finally { dispatch(new BusyChanged(false)); }
    }

    public static async Task LoginWithSsoAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, "正在打开 SSO 登录..."));
        try
        {
            var result = await BitwardenApplicationService.Instance.LoginWithSsoAsync();
            dispatch(new NoticeShown(result.Success ? "登录成功" : "登录失败", result.Message, result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
            if (result.Success) await InitializeAsync(dispatch);
        }
        finally { dispatch(new BusyChanged(false)); }
    }

    public static async Task LogoutActiveAccountAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, "正在退出账号..."));
        try
        {
            var success = await BitwardenApplicationService.Instance.LogoutAsync();
            dispatch(new NoticeShown(success ? "已退出账号" : "退出失败", success ? "当前账号的 CLI 会话已清除。" : "Bitwarden CLI 未能退出当前账号。", success ? InfoBarSeverity.Success : InfoBarSeverity.Error));
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
