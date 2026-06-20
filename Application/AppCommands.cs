using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace BitwardenForReactor.Application;

public static class AppCommands
{
    public static async Task InitializeAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, "检测 Bitwarden 状态..."));
        try
        {
            var status = await BitwardenCliService.GetStatusAsync();
            dispatch(new StatusLoaded(status));
            if (status?.IsUnlocked == true)
            {
                await LoadVaultAsync(dispatch);
            }
        }
        finally
        {
            dispatch(new BusyChanged(false));
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
            var result = await BitwardenCliService.Instance.UnlockAsync(masterPassword);
            if (!result.Success)
            {
                dispatch(new NoticeShown("解锁失败", result.Message, InfoBarSeverity.Error));
                setMasterPassword(string.Empty);
                return;
            }

            setMasterPassword(string.Empty);
            dispatch(new NoticeShown("已解锁", result.Message, InfoBarSeverity.Success));
            dispatch(new StatusLoaded(await BitwardenCliService.GetStatusAsync()));
            await LoadVaultAsync(dispatch);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task LoadVaultAsync(Action<AppAction> dispatch, string? selectedItemId = null)
    {
        dispatch(new BusyChanged(true, "正在加载密码库..."));
        try
        {
            var service = BitwardenCliService.Instance;
            var items = await service.GetItemsAsync() ?? [];
            var trash = await service.GetTrashItemsAsync() ?? [];
            var folders = await service.GetFoldersAsync() ?? [];
            dispatch(new VaultLoaded(items, trash, folders, selectedItemId));
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    public static async Task SyncAsync(Action<AppAction> dispatch)
    {
        dispatch(new BusyChanged(true, "正在同步..."));
        try
        {
            var success = await BitwardenCliService.Instance.SyncAsync();
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
            var success = await BitwardenCliService.Instance.LockAsync();
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
            var service = BitwardenCliService.Instance;
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
            var success = await BitwardenCliService.Instance.DeleteItemAsync(item.Id, permanent);
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
            var success = await BitwardenCliService.Instance.RestoreItemAsync(item.Id);
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
        var totp = await BitwardenCliService.Instance.GetTotpAsync(item.Id);
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
            var success = await BitwardenCliService.Instance.EditItemAsync(item.Id, update);
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
            var success = await BitwardenCliService.Instance.CloneItemAsync(item.Id, $"{item.Name} 副本");
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
        dispatch(new SettingsSaved(settings));
        dispatch(new NoticeShown("设置已保存", "新设置已生效。", InfoBarSeverity.Success));
    }
}
