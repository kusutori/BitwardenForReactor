using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using static Microsoft.UI.Reactor.Factories;

ReactorApp.Run<App>("BitwardenForReactor", width: 1120, height: 720);

class App : Component
{
    public override Element Render()
    {
        var (state, dispatch) = UseReducer<AppState, AppAction>(AppReducer.Reduce, new AppState());
        var (masterPassword, setMasterPassword) = UseState(string.Empty);

        UseEffect(() => { _ = InitializeAsync(dispatch); });

        return FlexColumn(
                TitleBar("BitwardenForReactor")
                    .RightHeader(RenderTitleActions(state, dispatch))
                    .Flex(shrink: 0),
                state.HasNotice ? RenderNotice(state, dispatch) : null,
                state.IsBusy ? RenderBusy(state) : null,
                state.IsUnlocked
                    ? RenderMain(state, dispatch)
                    : RenderUnlock(state, dispatch, masterPassword, setMasterPassword),
                RenderEditorDialog(state, dispatch),
                RenderDeleteDialog(state, dispatch))
            .Backdrop(BackdropKind.Mica);
    }

    private static Element RenderTitleActions(AppState state, Action<AppAction> dispatch) =>
        HStack(8,
            Button("同步", () => _ = SyncAsync(dispatch)).IsEnabled(state.IsUnlocked && !state.IsBusy).AutomationName("同步密码库"),
            Button("锁定", () => _ = LockAsync(dispatch)).IsEnabled(state.IsUnlocked && !state.IsBusy).AutomationName("锁定密码库"));

    private static Element RenderNotice(AppState state, Action<AppAction> dispatch) =>
        InfoBar(state.NoticeTitle, state.NoticeMessage)
            .Severity(state.NoticeSeverity)
            .IsClosable(true)
            .Closed(() => dispatch(new NoticeCleared()))
            .Margin(left: 12, top: 8, right: 12, bottom: 0);

    private static Element RenderBusy(AppState state) =>
        Border(
            HStack(10,
                ProgressRing().Width(22).Height(22).Set(ring => ring.IsActive = true),
                TextBlock(string.IsNullOrWhiteSpace(state.BusyText) ? "处理中..." : state.BusyText)
                    .Foreground(Theme.SecondaryText)))
            .Padding(12)
            .Background(Theme.SubtleFill);

    private static Element RenderUnlock(
        AppState state,
        Action<AppAction> dispatch,
        string masterPassword,
        Action<string> setMasterPassword)
    {
        var status = state.Status;
        Element statusBanner = status is null
            ? InfoBar("未检测到 Bitwarden CLI", "请安装 Bitwarden CLI，或在设置中配置 bw.exe 路径。")
                .Severity(InfoBarSeverity.Error)
            : !status.IsLoggedIn
                ? InfoBar("尚未登录", "请先在终端执行 bw login，然后回到此应用解锁。")
                    .Severity(InfoBarSeverity.Warning)
                : TextBlock(status.UserEmail ?? "已登录").Foreground(Theme.SecondaryText);

        return Border(
                FlexColumn(
                    Icon(FontIcon("\uE72E", fontSize: 58)),
                    Heading("Bitwarden"),
                    statusBanner,
                    PasswordBox(masterPassword, setMasterPassword, "输入主密码")
                        .Header("主密码")
                        .OnKeyDown((_, e) =>
                        {
                            if (e.Key == VirtualKey.Enter)
                            {
                                _ = UnlockAsync(masterPassword, setMasterPassword, dispatch);
                            }
                        })
                        .IsEnabled(!state.IsBusy)
                        .AutomationName("主密码"),
                    Button("解锁", () => _ = UnlockAsync(masterPassword, setMasterPassword, dispatch))
                        .IsEnabled(!state.IsBusy && !string.IsNullOrWhiteSpace(masterPassword))
                        .Background(Theme.Accent)
                        .AutomationName("解锁密码库"),
                    HyperlinkButton("重新检测状态", onClick: () => _ = InitializeAsync(dispatch))
                        .AutomationName("重新检测状态"))
                with { RowGap = 16, AlignItems = FlexAlign.Center })
            .Padding(32)
            .MaxWidth(440)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Flex(grow: 1, basis: 0);
    }

    private static Element RenderMain(AppState state, Action<AppAction> dispatch)
    {
        var nav = NavigationView(
                [
                    NavItem("全部项目", "Library", "AllItems"),
                    NavItem("登录", "Contact", "Logins"),
                    NavItem("卡片", "ContactInfo", "Cards"),
                    NavItem("身份", "People", "Identities"),
                    NavItem("笔记", "Page", "Notes"),
                    NavItem("收藏", "Favorite", "Favorites"),
                    NavItem("回收站", "Delete", "Trash"),
                    NavItem("设置", "Setting", "Settings")
                ],
                state.ShowSettings ? RenderSettings(state, dispatch) : RenderVault(state, dispatch))
            .PaneTitle("密码库")
            .OpenPaneLength(210)
            .PaneDisplayMode(NavigationViewPaneDisplayMode.Left)
            .SelectedTagChanged(tag =>
            {
                if (tag == "Settings")
                {
                    dispatch(new SettingsVisibilityChanged(true));
                    return;
                }

                dispatch(new FilterChanged(TagToFilter(tag ?? "AllItems")));
            })
            .PaneFooter(
                VStack(8,
                    Button("新建项目", () => dispatch(new EditorOpened(VaultItemDraft.New())))
                        .AutomationName("新建项目"),
                    Button("同步", () => _ = SyncAsync(dispatch)).AutomationName("同步密码库"),
                    Button("锁定", () => _ = LockAsync(dispatch)).AutomationName("锁定密码库"))
                .Padding(8));

        return (nav with
        {
            SelectedTag = state.ShowSettings ? "Settings" : FilterToTag(state.Filter),
            IsSettingsVisible = false
        }).Flex(grow: 1, basis: 0);
    }

    private static Element RenderVault(AppState state, Action<AppAction> dispatch) =>
        FlexRow(
                RenderListPane(state, dispatch).Flex(shrink: 0, basis: 360),
                RenderDetailPane(state, dispatch).Flex(grow: 1, basis: 0))
            .Flex(grow: 1, basis: 0);

    private static Element RenderListPane(AppState state, Action<AppAction> dispatch)
    {
        var items = state.VisibleItems;
        var selectedId = state.SelectedItem?.Id;

        return Border(
                FlexColumn(
                    AutoSuggestBox(state.SearchQuery, query => dispatch(new SearchChanged(query)))
                        .PlaceholderText("搜索密码库...")
                        .QueryIcon(SymbolIcon("Find"))
                        .AutomationName("搜索密码库")
                        .Margin(12),
                    items.Count == 0
                        ? RenderEmptyList(state)
                        : (ListView(items, item => item.Id, (item, _) => RenderListItem(item, item.Id == selectedId))
                            with { SelectionMode = ListViewSelectionMode.Single })
                            .SelectionChanged<BitwardenItem>(selected =>
                            {
                                var item = selected.FirstOrDefault();
                                dispatch(new ItemSelected(item?.Id));
                            })
                            .Flex(grow: 1, basis: 0),
                    Border(TextBlock($"{items.Count} 个项目").Foreground(Theme.SecondaryText))
                        .Padding(12)
                        .Background(Theme.SubtleFill)
                        .Flex(shrink: 0))
                .Flex(grow: 1, basis: 0))
            .WithBorder(Theme.DividerStroke, 1)
            .Flex(grow: 1, basis: 0);
    }

    private static Element RenderEmptyList(AppState state) =>
        Border(
                VStack(10,
                    Icon(FontIcon("\uE8F1", fontSize: 34)),
                    TextBlock(state.Filter == VaultFilter.Trash ? "回收站为空" : "没有匹配的项目")
                        .Foreground(Theme.SecondaryText)))
            .Flex(grow: 1, basis: 0)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);

    private static Element RenderListItem(BitwardenItem item, bool selected)
    {
        var subtitle = item.Username ?? item.PrimaryUri ?? item.TypeLabel;

        return Border(
                HStack(12,
                    Border(Icon(FontIcon(IconService.GetItemTypeGlyph(item.Type), fontSize: 18)))
                        .Width(40)
                        .Height(40)
                        .CornerRadius(20)
                        .Background(Theme.SubtleFill),
                    VStack(2,
                        TextBlock(item.Name).TextTrimming(TextTrimming.CharacterEllipsis),
                        TextBlock(subtitle).Foreground(Theme.SecondaryText).TextTrimming(TextTrimming.CharacterEllipsis))
                    .Flex(grow: 1, basis: 0),
                    item.Favorite
                        ? Icon(FontIcon("\uE735", fontSize: 14)).Foreground(Theme.SystemCaution)
                        : null)
                .VerticalAlignment(VerticalAlignment.Center))
            .Padding(left: 12, top: 8, right: 12, bottom: 8)
            .Background(selected ? Theme.ControlFillSecondary : Theme.SolidBackground);
    }

    private static Element RenderDetailPane(AppState state, Action<AppAction> dispatch)
    {
        var item = state.SelectedItem;
        if (item is null)
        {
            return Border(
                    VStack(12,
                        Icon(FontIcon("\uE785", fontSize: 42)),
                        TextBlock("选择一个项目查看详情").Foreground(Theme.SecondaryText)))
                .HorizontalAlignment(HorizontalAlignment.Center)
                .VerticalAlignment(VerticalAlignment.Center)
                .Flex(grow: 1, basis: 0);
        }

        return FlexColumn(
                Border(
                    HStack(12,
                        VStack(2,
                            SubHeading(item.Name),
                            Caption(item.TypeLabel).Foreground(Theme.SecondaryText))
                        .Flex(grow: 1, basis: 0),
                        Button(Icon(FontIcon("\uE70F")), () => dispatch(new EditorOpened(VaultItemDraft.FromItem(item))))
                            .ToolTip("编辑")
                            .AutomationName("编辑项目"),
                        state.Filter == VaultFilter.Trash
                            ? Button(Icon(FontIcon("\uE845")), () => _ = RestoreAsync(item, dispatch))
                                .ToolTip("恢复")
                                .AutomationName("恢复项目")
                            : Button(Icon(FontIcon("\uE74D")), () => dispatch(new DeleteRequested(item, false)))
                                .ToolTip("删除")
                                .AutomationName("删除项目"),
                        state.Filter == VaultFilter.Trash
                            ? Button(Icon(FontIcon("\uE74D")), () => dispatch(new DeleteRequested(item, true)))
                                .ToolTip("永久删除")
                                .AutomationName("永久删除项目")
                            : null))
                .Padding(16)
                .WithBorder(Theme.DividerStroke, 1)
                .Flex(shrink: 0),
                ScrollView(
                    VStack(12,
                        RenderItemHeader(item),
                        RenderItemFields(item, dispatch),
                        string.IsNullOrWhiteSpace(item.Notes) ? null : RenderSection("备注", TextBlock(item.Notes).TextWrapping())))
                .Padding(20)
                .Flex(grow: 1, basis: 0))
            .Background(Theme.LayerFill)
            .Flex(grow: 1, basis: 0);
    }

    private static Element RenderItemHeader(BitwardenItem item) =>
        HStack(14,
            Border(Icon(FontIcon(IconService.GetItemTypeGlyph(item.Type), fontSize: 28)))
                .Width(52)
                .Height(52)
                .CornerRadius(26)
                .Background(Theme.SubtleFill),
            VStack(2,
                Heading(item.Name),
                TextBlock(item.TypeLabel).Foreground(Theme.SecondaryText)),
            item.Favorite ? Icon(FontIcon("\uE735", fontSize: 18)).Foreground(Theme.SystemCaution) : null);

    private static Element RenderItemFields(BitwardenItem item, Action<AppAction> dispatch)
    {
        var fields = new List<Element>();

        switch (item.Type)
        {
            case BitwardenItemType.Login:
                AddField(fields, "用户名", item.Login?.Username, dispatch);
                AddField(fields, "密码", Mask("password"), dispatch, item.Login?.Password);
                foreach (var uri in item.Login?.Uris ?? [])
                {
                    AddField(fields, "网站", uri.Uri, dispatch);
                }
                if (!string.IsNullOrWhiteSpace(item.Login?.Totp))
                {
                    fields.Add(RenderField("TOTP", "点击获取验证码", Button("获取", () => _ = CopyTotpAsync(item, dispatch)).AutomationName("复制TOTP")));
                }
                break;
            case BitwardenItemType.Card:
                AddField(fields, "品牌", item.Card?.Brand, dispatch);
                AddField(fields, "持卡人", item.Card?.CardholderName, dispatch);
                AddField(fields, "卡号", MaskCard(item.Card?.Number), dispatch, item.Card?.Number);
                AddField(fields, "有效期", FormatExpiry(item.Card), dispatch);
                AddField(fields, "CVV", Mask("cvv"), dispatch, item.Card?.Code);
                break;
            case BitwardenItemType.Identity:
                AddField(fields, "姓名", JoinParts(item.Identity?.FirstName, item.Identity?.LastName), dispatch);
                AddField(fields, "邮箱", item.Identity?.Email, dispatch);
                AddField(fields, "电话", item.Identity?.Phone, dispatch);
                AddField(fields, "公司", item.Identity?.Company, dispatch);
                AddField(fields, "地址", JoinParts(item.Identity?.Address1, item.Identity?.Address2, item.Identity?.Address3, item.Identity?.City, item.Identity?.State, item.Identity?.PostalCode, item.Identity?.Country), dispatch);
                AddField(fields, "SSN", Mask("ssn"), dispatch, item.Identity?.Ssn);
                AddField(fields, "护照", item.Identity?.PassportNumber, dispatch);
                AddField(fields, "驾照", item.Identity?.LicenseNumber, dispatch);
                break;
            case BitwardenItemType.SecureNote:
                fields.Add(RenderSection("安全笔记", TextBlock("内容在备注区域显示。").Foreground(Theme.SecondaryText)));
                break;
        }

        if (item.Fields?.Count > 0)
        {
            fields.Add(RenderSection("自定义字段", VStack(8, item.Fields.Select(field =>
                RenderField(
                    field.Name,
                    field.Type == CustomFieldType.Hidden ? Mask("hidden") : field.Value ?? string.Empty,
                    CopyButton(field.Value, dispatch))
                .WithKey(field.Name)).ToArray())));
        }

        return VStack(10, fields.ToArray());
    }

    private static void AddField(List<Element> fields, string label, string? value, Action<AppAction> dispatch, string? copyValue = null)
    {
        if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(copyValue))
        {
            return;
        }

        fields.Add(RenderField(label, value ?? string.Empty, CopyButton(copyValue ?? value, dispatch)));
    }

    private static Element RenderField(string label, string value, Element? trailing) =>
        Border(
                VStack(4,
                    TextBlock(label).Foreground(Theme.SecondaryText),
                    HStack(8,
                        TextBlock(value).TextWrapping().Flex(grow: 1, basis: 0),
                        trailing)))
            .Padding(12)
            .Background(Theme.CardBackground)
            .CornerRadius(6)
            .WithBorder(Theme.CardStroke, 1);

    private static Element CopyButton(string? value, Action<AppAction> dispatch) =>
        string.IsNullOrWhiteSpace(value)
            ? Empty()
            : Button(Icon(FontIcon("\uE8C8")), () => _ = CopyAsync(value, dispatch))
                .ToolTip("复制")
                .AutomationName("复制字段");

    private static Element RenderSection(string title, Element content) =>
        VStack(8,
            TextBlock(title).SemiBold(),
            content);

    private static Element RenderSettings(AppState state, Action<AppAction> dispatch)
    {
        var settings = state.Settings;

        return ScrollView(
                Border(
                    VStack(14,
                        Heading("设置"),
                        TextBox(settings.BwPath, value => dispatch(new SettingsChanged(settings with { BwPath = value })), header: "Bitwarden CLI 路径")
                            .AutomationName("Bitwarden CLI 路径"),
                        TextBox(settings.BwClientId, value => dispatch(new SettingsChanged(settings with { BwClientId = value })), header: "BW_CLIENTID")
                            .AutomationName("BW_CLIENTID"),
                        PasswordBox(settings.BwClientSecret, value => dispatch(new SettingsChanged(settings with { BwClientSecret = value })), "输入 Client Secret")
                            .Header("BW_CLIENTSECRET")
                            .AutomationName("BW_CLIENTSECRET"),
                        TextBox(settings.CustomEnvironment, value => dispatch(new SettingsChanged(settings with { CustomEnvironment = value })), header: "自定义环境变量")
                            .PlaceholderText("KEY1=VALUE1;KEY2=VALUE2")
                            .AutomationName("自定义环境变量"),
                        TextBox(settings.ClipboardClearSeconds.ToString(CultureInfo.InvariantCulture),
                                value => dispatch(new SettingsChanged(settings with { ClipboardClearSeconds = ParsePositiveInt(value, settings.ClipboardClearSeconds) })),
                                header: "剪贴板自动清除秒数")
                            .AutomationName("剪贴板自动清除秒数"),
                        TextBox(settings.AutoLockMinutes.ToString(CultureInfo.InvariantCulture),
                                value => dispatch(new SettingsChanged(settings with { AutoLockMinutes = ParsePositiveInt(value, settings.AutoLockMinutes) })),
                                header: "自动锁定分钟数")
                            .AutomationName("自动锁定分钟数"),
                        HStack(8,
                            Button("保存设置", () => _ = SaveSettingsAsync(state.Settings, dispatch)).Background(Theme.Accent).AutomationName("保存设置"),
                            Button("检测状态", () => _ = InitializeAsync(dispatch)).AutomationName("检测状态"))))
                .Padding(24)
                .MaxWidth(720))
            .Flex(grow: 1, basis: 0);
    }

    private static Element? RenderEditorDialog(AppState state, Action<AppAction> dispatch)
    {
        var draft = state.EditorDraft;
        if (draft is null)
        {
            return null;
        }

        var title = draft.Id is null ? "新建项目" : "编辑项目";
        return ContentDialog(title, RenderEditorForm(draft, dispatch), "保存") with
        {
            IsOpen = true,
            SecondaryButtonText = "取消",
            CloseButtonText = string.Empty,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(draft.Name),
            DefaultButton = ContentDialogButton.Primary,
            OnClosed = result =>
            {
                if (result == ContentDialogResult.Primary)
                {
                    _ = SaveDraftAsync(draft, dispatch);
                }
                else
                {
                    dispatch(new EditorClosed());
                }
            }
        };
    }

    private static Element RenderEditorForm(VaultItemDraft draft, Action<AppAction> dispatch)
    {
        void Update(VaultItemDraft changed) => dispatch(new EditorDraftChanged(changed));

        var typeNames = new[] { "登录", "安全笔记", "卡片", "身份" };
        var typeValues = new[] { BitwardenItemType.Login, BitwardenItemType.SecureNote, BitwardenItemType.Card, BitwardenItemType.Identity };
        var selectedType = Array.IndexOf(typeValues, draft.Type);

        return ScrollView(
            VStack(12,
                ComboBox(typeNames, selectedType, index => Update(draft with { Type = typeValues[Math.Max(0, index)] }))
                    .Header("类型")
                    .AutomationName("项目类型"),
                TextBox(draft.Name, value => Update(draft with { Name = value }), header: "名称").AutomationName("名称"),
                CheckBox(draft.Favorite, value => Update(draft with { Favorite = value }), "收藏").AutomationName("收藏"),
                draft.Type == BitwardenItemType.Login ? RenderLoginEditor(draft, Update) : null,
                draft.Type == BitwardenItemType.Card ? RenderCardEditor(draft, Update) : null,
                draft.Type == BitwardenItemType.Identity ? RenderIdentityEditor(draft, Update) : null,
                TextBox(draft.Notes ?? string.Empty, value => Update(draft with { Notes = value }), header: "备注")
                    .TextWrapping()
                    .Set(box =>
                    {
                        box.AcceptsReturn = true;
                        box.MinHeight = 100;
                    })
                    .AutomationName("备注")))
            .Width(520);
    }

    private static Element RenderLoginEditor(VaultItemDraft draft, Action<VaultItemDraft> update) =>
        VStack(10,
            TextBox(draft.Username ?? string.Empty, value => update(draft with { Username = value }), header: "用户名").AutomationName("用户名"),
            PasswordBox(draft.Password ?? string.Empty, value => update(draft with { Password = value }), "密码")
                .Header("密码")
                .AutomationName("密码"),
            TextBox(draft.Uri ?? string.Empty, value => update(draft with { Uri = value }), header: "网站").AutomationName("网站"));

    private static Element RenderCardEditor(VaultItemDraft draft, Action<VaultItemDraft> update) =>
        VStack(10,
            TextBox(draft.CardBrand ?? string.Empty, value => update(draft with { CardBrand = value }), header: "品牌").AutomationName("品牌"),
            TextBox(draft.CardholderName ?? string.Empty, value => update(draft with { CardholderName = value }), header: "持卡人").AutomationName("持卡人"),
            TextBox(draft.CardNumber ?? string.Empty, value => update(draft with { CardNumber = value }), header: "卡号").AutomationName("卡号"),
            HStack(8,
                TextBox(draft.CardExpMonth ?? string.Empty, value => update(draft with { CardExpMonth = value }), header: "月份").Flex(grow: 1, basis: 0).AutomationName("月份"),
                TextBox(draft.CardExpYear ?? string.Empty, value => update(draft with { CardExpYear = value }), header: "年份").Flex(grow: 1, basis: 0).AutomationName("年份")),
            PasswordBox(draft.CardCode ?? string.Empty, value => update(draft with { CardCode = value }), "CVV").Header("CVV").AutomationName("CVV"));

    private static Element RenderIdentityEditor(VaultItemDraft draft, Action<VaultItemDraft> update) =>
        VStack(10,
            HStack(8,
                TextBox(draft.FirstName ?? string.Empty, value => update(draft with { FirstName = value }), header: "名").Flex(grow: 1, basis: 0).AutomationName("名"),
                TextBox(draft.LastName ?? string.Empty, value => update(draft with { LastName = value }), header: "姓").Flex(grow: 1, basis: 0).AutomationName("姓")),
            TextBox(draft.Email ?? string.Empty, value => update(draft with { Email = value }), header: "邮箱").AutomationName("邮箱"),
            TextBox(draft.Phone ?? string.Empty, value => update(draft with { Phone = value }), header: "电话").AutomationName("电话"),
            TextBox(draft.Company ?? string.Empty, value => update(draft with { Company = value }), header: "公司").AutomationName("公司"),
            TextBox(draft.Address ?? string.Empty, value => update(draft with { Address = value }), header: "地址").AutomationName("地址"));

    private static Element? RenderDeleteDialog(AppState state, Action<AppAction> dispatch)
    {
        var target = state.DeleteTarget;
        if (target is null)
        {
            return null;
        }

        var message = state.DeletePermanently
            ? $"确定要永久删除「{target.Name}」吗？此操作无法撤销。"
            : $"确定要将「{target.Name}」移入回收站吗？";

        return ContentDialog("确认删除", TextBlock(message).TextWrapping(), state.DeletePermanently ? "永久删除" : "删除") with
        {
            IsOpen = true,
            SecondaryButtonText = "取消",
            CloseButtonText = string.Empty,
            DefaultButton = ContentDialogButton.Secondary,
            OnClosed = result =>
            {
                if (result == ContentDialogResult.Primary)
                {
                    _ = DeleteAsync(target, state.DeletePermanently, dispatch);
                }
                else
                {
                    dispatch(new DeleteCancelled());
                }
            }
        };
    }

    private static async Task InitializeAsync(Action<AppAction> dispatch)
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

    private static async Task UnlockAsync(string masterPassword, Action<string> setMasterPassword, Action<AppAction> dispatch)
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
            var status = await BitwardenCliService.GetStatusAsync();
            dispatch(new StatusLoaded(status));
            await LoadVaultAsync(dispatch);
        }
        finally
        {
            dispatch(new BusyChanged(false));
        }
    }

    private static async Task LoadVaultAsync(Action<AppAction> dispatch, string? selectedItemId = null)
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

    private static async Task SyncAsync(Action<AppAction> dispatch)
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

    private static async Task LockAsync(Action<AppAction> dispatch)
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

    private static async Task SaveDraftAsync(VaultItemDraft draft, Action<AppAction> dispatch)
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

    private static async Task DeleteAsync(BitwardenItem item, bool permanent, Action<AppAction> dispatch)
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

    private static async Task RestoreAsync(BitwardenItem item, Action<AppAction> dispatch)
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

    private static async Task CopyTotpAsync(BitwardenItem item, Action<AppAction> dispatch)
    {
        var totp = await BitwardenCliService.Instance.GetTotpAsync(item.Id);
        if (string.IsNullOrWhiteSpace(totp))
        {
            dispatch(new NoticeShown("TOTP 不可用", "未能获取验证码。", InfoBarSeverity.Warning));
            return;
        }

        await CopyAsync(totp, dispatch);
    }

    private static async Task CopyAsync(string value, Action<AppAction> dispatch)
    {
        await ClipboardService.CopyToClipboardWithTimeoutAsync(value, SettingsManager.Instance.Current.ClipboardClearSeconds);
        dispatch(new NoticeShown("已复制", "内容已复制到剪贴板，并会按设置自动清除。", InfoBarSeverity.Success));
    }

    private static async Task SaveSettingsAsync(AppSettings settings, Action<AppAction> dispatch)
    {
        await SettingsManager.Instance.SaveAsync(settings);
        dispatch(new SettingsSaved(settings));
        dispatch(new NoticeShown("设置已保存", "新设置已生效。", InfoBarSeverity.Success));
    }

    private static VaultFilter TagToFilter(string tag) =>
        tag switch
        {
            "Logins" => VaultFilter.Logins,
            "Cards" => VaultFilter.Cards,
            "Identities" => VaultFilter.Identities,
            "Notes" => VaultFilter.Notes,
            "Favorites" => VaultFilter.Favorites,
            "Trash" => VaultFilter.Trash,
            _ => VaultFilter.AllItems
        };

    private static string FilterToTag(VaultFilter filter) =>
        filter switch
        {
            VaultFilter.Logins => "Logins",
            VaultFilter.Cards => "Cards",
            VaultFilter.Identities => "Identities",
            VaultFilter.Notes => "Notes",
            VaultFilter.Favorites => "Favorites",
            VaultFilter.Trash => "Trash",
            _ => "AllItems"
        };

    private static string Mask(string kind) =>
        kind switch
        {
            "cvv" => "***",
            "ssn" => "***-**-****",
            _ => "••••••••"
        };

    private static string? MaskCard(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        return number.Length > 4 ? $"•••• •••• •••• {number[^4..]}" : number;
    }

    private static string? FormatExpiry(CardData? card)
    {
        if (card is null || (string.IsNullOrWhiteSpace(card.ExpMonth) && string.IsNullOrWhiteSpace(card.ExpYear)))
        {
            return null;
        }

        return $"{card.ExpMonth}/{card.ExpYear}";
    }

    private static string? JoinParts(params string?[] parts)
    {
        var text = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int ParsePositiveInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : fallback;
}
