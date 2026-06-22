using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Application;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hooks;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static BitwardenForReactor.Controls.Toolkit.ToolkitFactories;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record ItemEditorDialogProps(
    VaultItemDraft Draft,
    IReadOnlyList<BitwardenFolder> Folders,
    Action<AppAction> Dispatch);

public sealed class ItemEditorDialog : Component<ItemEditorDialogProps>
{
    public override Element Render() =>
        Border(
            Component<ItemEditorForm, ItemEditorFormProps>(
                new ItemEditorFormProps(
                    Props.Draft,
                    Props.Folders,
                    draft => _ = AppCommands.SaveDraftAsync(draft, Props.Dispatch),
                    () => Props.Dispatch(new EditorClosed())))
                .Margin(24)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .VerticalAlignment(VerticalAlignment.Center))
            .Background(Theme.SmokeFill)
            .AutomationName("项目编辑器遮罩");
}

internal sealed record ItemEditorFormProps(
    VaultItemDraft InitialDraft,
    IReadOnlyList<BitwardenFolder> Folders,
    Action<VaultItemDraft> OnSave,
    Action OnCancel);

internal sealed class ItemEditorForm : Component<ItemEditorFormProps>
{
    public override Element Render()
    {
        var (draft, setDraft) = UseReducer(Props.InitialDraft);
        var formScroll = this.UseElementRef<Microsoft.UI.Xaml.Controls.ScrollView>();

        void Update(Func<VaultItemDraft, VaultItemDraft> change) =>
            setDraft(change);

        var typeNames = new[] { "登录", "安全笔记", "卡片", "身份" };
        var typeValues = new[] { BitwardenItemType.Login, BitwardenItemType.SecureNote, BitwardenItemType.Card, BitwardenItemType.Identity };
        var selectedType = Array.IndexOf(typeValues, draft.Type);
        var folderNames = new[] { "无文件夹" }.Concat(Props.Folders.Select(folder => folder.Name)).ToArray();
        var selectedFolder = string.IsNullOrWhiteSpace(draft.FolderId)
            ? 0
            : Math.Max(0, Props.Folders.ToList().FindIndex(folder => folder.Id == draft.FolderId) + 1);

        var form = ScrollView(
                VStack(16,
                EditorSection("基础信息",
                [
                    VStack(6,
                        TextBlock("类型").Foreground(Theme.SecondaryText),
                        Segmented(typeNames, selectedType, index =>
                        {
                            Update(current => current with
                            {
                                Type = typeValues[Math.Clamp(index, 0, typeValues.Length - 1)]
                            });
                            formScroll.Current?.ScrollTo(0, 0);
                        })
                            .AutomationName("项目类型")),
                    TextBox(draft.Name, value => Update(current => current with { Name = value }), header: "名称")
                        .AutomationName("名称"),
                    ComboBox(folderNames, selectedFolder, index =>
                            Update(current => current with
                            {
                                FolderId = index <= 0 || index > Props.Folders.Count
                                    ? null
                                    : Props.Folders[index - 1].Id
                            }))
                        .Header("文件夹")
                        .HorizontalAlignment(HorizontalAlignment.Stretch)
                        .AutomationName("文件夹"),
                    string.IsNullOrWhiteSpace(draft.Name)
                        ? TextBlock("名称必填。").Foreground(Theme.SystemCaution)
                        : null
                ]),
                draft.Type == BitwardenItemType.Login ? RenderLogin(draft, Update) : null,
                draft.Type == BitwardenItemType.Card ? RenderCard(draft, Update) : null,
                draft.Type == BitwardenItemType.Identity ? RenderIdentity(draft, Update) : null,
                draft.Type == BitwardenItemType.SecureNote ? RenderSecureNote() : null,
                EditorSection("备注与选项",
                [
                    TextBox(draft.Notes ?? string.Empty, value => Update(current => current with { Notes = value }), header: "备注")
                        .TextWrapping()
                        .AcceptsReturn()
                        .MinHeight(100)
                        .AutomationName("备注"),
                    CheckBox(draft.Favorite, value => Update(current => current with { Favorite = value }), "收藏")
                        .AutomationName("收藏")
                ])))
            .Padding(24)
            .Ref(formScroll)
            .Grid(row: 1);

        return Border(
                Grid(
                    columns: [GridSize.Star()],
                    rows: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                    Heading(draft.Id is null ? "新建项目" : "编辑项目")
                        .Margin(left: 24, top: 20, right: 24, bottom: 12)
                        .Grid(row: 0),
                    form,
                    Border(
                            HStack(12,
                                Button("取消", Props.OnCancel)
                                    .MinWidth(96)
                                    .AutomationName("取消编辑"),
                                Button("保存", () => Props.OnSave(draft))
                                    .MinWidth(96)
                                    .IsEnabled(!string.IsNullOrWhiteSpace(draft.Name))
                                    .AutomationName("保存项目"))
                                .HorizontalAlignment(HorizontalAlignment.Right))
                        .WithBorder(Theme.CardStroke, 1)
                        .Padding(16)
                        .Grid(row: 2)))
            .Background(Theme.SolidBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(8)
            .MinWidth(420)
            .MaxWidth(560)
            .MaxHeight(680)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .AutomationName("项目编辑器");
    }

    private static Element RenderLogin(VaultItemDraft draft, Action<Func<VaultItemDraft, VaultItemDraft>> update)
    {
        var uriRows = draft.Uris.Select(uri =>
            Grid(
                columns: [GridSize.Star(), GridSize.Auto],
                rows: [GridSize.Auto],
                TextBox(uri.Value, value => update(current => current with
                    {
                        Uris = current.Uris.Select(entry => entry.Key == uri.Key ? entry with { Value = value } : entry).ToArray()
                    }), header: "网站 (URI)")
                    .AutomationName("网站 URI")
                    .Grid(column: 0),
                Button(Icon(FontIcon("\uE74D")), () => update(current => current with
                    {
                        Uris = current.Uris.Where(entry => entry.Key != uri.Key).ToArray()
                    }))
                    .IsEnabled(draft.Uris.Count > 1)
                    .AutomationName("删除网站")
                    .Margin(left: 8, top: 24, right: 0, bottom: 0)
                    .Grid(column: 1))
                .WithKey(uri.Key.ToString()))
            .ToArray();

        return EditorSection("登录信息",
        [
            TextBox(draft.Username ?? string.Empty, value => update(current => current with { Username = value }), header: "用户名").AutomationName("用户名"),
            PasswordBox(draft.Password ?? string.Empty, value => update(current => current with { Password = value }), "密码")
                .Header("密码")
                .AutomationName("密码"),
            VStack(8, uriRows
                .Cast<Element>()
                .Append(
                    Button("+  添加网站", () => update(current => current with { Uris = [.. current.Uris, VaultUriDraft.New()] }))
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .AutomationName("添加网站"))
                .ToArray())
        ]);
    }

    private static Element RenderCard(VaultItemDraft draft, Action<Func<VaultItemDraft, VaultItemDraft>> update) =>
        EditorSection("卡片信息",
        [
            TextBox(draft.CardBrand ?? string.Empty, value => update(current => current with { CardBrand = value }), header: "品牌").AutomationName("品牌"),
            TextBox(draft.CardholderName ?? string.Empty, value => update(current => current with { CardholderName = value }), header: "持卡人").AutomationName("持卡人"),
            TextBox(draft.CardNumber ?? string.Empty, value => update(current => current with { CardNumber = value }), header: "卡号").AutomationName("卡号"),
            HStack(8,
                TextBox(draft.CardExpMonth ?? string.Empty, value => update(current => current with { CardExpMonth = value }), header: "月份")
                    .Flex(grow: 1, basis: 0).AutomationName("月份"),
                TextBox(draft.CardExpYear ?? string.Empty, value => update(current => current with { CardExpYear = value }), header: "年份")
                    .Flex(grow: 1, basis: 0).AutomationName("年份")),
            PasswordBox(draft.CardCode ?? string.Empty, value => update(current => current with { CardCode = value }), "CVV")
                .Header("CVV").AutomationName("CVV")
        ]);

    private static Element RenderIdentity(VaultItemDraft draft, Action<Func<VaultItemDraft, VaultItemDraft>> update) =>
        EditorSection("身份信息",
        [
            HStack(8,
                TextBox(draft.FirstName ?? string.Empty, value => update(current => current with { FirstName = value }), header: "名")
                    .Flex(grow: 1, basis: 0).AutomationName("名"),
                TextBox(draft.LastName ?? string.Empty, value => update(current => current with { LastName = value }), header: "姓")
                    .Flex(grow: 1, basis: 0).AutomationName("姓")),
            TextBox(draft.Email ?? string.Empty, value => update(current => current with { Email = value }), header: "邮箱").AutomationName("邮箱"),
            TextBox(draft.Phone ?? string.Empty, value => update(current => current with { Phone = value }), header: "电话").AutomationName("电话"),
            TextBox(draft.Company ?? string.Empty, value => update(current => current with { Company = value }), header: "公司").AutomationName("公司"),
            TextBox(draft.Address ?? string.Empty, value => update(current => current with { Address = value }), header: "地址").AutomationName("地址")
        ]);

    private static Element RenderSecureNote() =>
        EditorSection("安全笔记",
        [
            TextBlock("安全笔记只需要名称和备注内容。")
                .Foreground(Theme.SecondaryText)
                .TextWrapping()
        ]);

    private static Element EditorSection(string title, IReadOnlyList<Element?> children) =>
        VStack(8,
            TextBlock(title).SemiBold(),
            VStack(10, children.Where(child => child is not null).Cast<Element>().ToArray()));
}
