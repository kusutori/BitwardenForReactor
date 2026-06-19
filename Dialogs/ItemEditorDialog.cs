using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Application;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml.Controls;
using static BitwardenForReactor.Controls.Toolkit.ToolkitFactories;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record ItemEditorDialogProps(VaultItemDraft Draft, Action<AppAction> Dispatch);

public sealed class ItemEditorDialog : Component<ItemEditorDialogProps>
{
    public override Element Render()
    {
        var draft = Props.Draft;
        var title = draft.Id is null ? "新建项目" : "编辑项目";
        return ContentDialog(title, RenderForm(draft), "保存") with
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
                    _ = AppCommands.SaveDraftAsync(draft, Props.Dispatch);
                }
                else
                {
                    Props.Dispatch(new EditorClosed());
                }
            }
        };
    }

    private Element RenderForm(VaultItemDraft draft)
    {
        void Update(VaultItemDraft changed) => Props.Dispatch(new EditorDraftChanged(changed));

        var typeNames = new[] { "登录", "安全笔记", "卡片", "身份" };
        var typeValues = new[] { BitwardenItemType.Login, BitwardenItemType.SecureNote, BitwardenItemType.Card, BitwardenItemType.Identity };
        var selectedType = Array.IndexOf(typeValues, draft.Type);

        return ScrollView(
            VStack(16,
                EditorSection("基础信息",
                [
                    VStack(6,
                        TextBlock("类型").Foreground(Theme.SecondaryText),
                        Segmented(typeNames, selectedType, index =>
                            Update(draft with { Type = typeValues[Math.Clamp(index, 0, typeValues.Length - 1)] }))
                            .AutomationName("项目类型")),
                    TextBox(draft.Name, value => Update(draft with { Name = value }), header: "名称")
                        .AutomationName("名称"),
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
                    TextBox(draft.Notes ?? string.Empty, value => Update(draft with { Notes = value }), header: "备注")
                        .TextWrapping()
                        .Set(box =>
                        {
                            box.AcceptsReturn = true;
                            box.MinHeight = 100;
                        })
                        .AutomationName("备注"),
                    CheckBox(draft.Favorite, value => Update(draft with { Favorite = value }), "收藏")
                        .AutomationName("收藏")
                ])))
            .Width(520);
    }

    private static Element RenderLogin(VaultItemDraft draft, Action<VaultItemDraft> update) =>
        EditorSection("登录信息",
        [
            TextBox(draft.Username ?? string.Empty, value => update(draft with { Username = value }), header: "用户名").AutomationName("用户名"),
            PasswordBox(draft.Password ?? string.Empty, value => update(draft with { Password = value }), "密码")
                .Header("密码")
                .AutomationName("密码"),
            TextBox(draft.Uri ?? string.Empty, value => update(draft with { Uri = value }), header: "网站").AutomationName("网站")
        ]);

    private static Element RenderCard(VaultItemDraft draft, Action<VaultItemDraft> update) =>
        EditorSection("卡片信息",
        [
            TextBox(draft.CardBrand ?? string.Empty, value => update(draft with { CardBrand = value }), header: "品牌").AutomationName("品牌"),
            TextBox(draft.CardholderName ?? string.Empty, value => update(draft with { CardholderName = value }), header: "持卡人").AutomationName("持卡人"),
            TextBox(draft.CardNumber ?? string.Empty, value => update(draft with { CardNumber = value }), header: "卡号").AutomationName("卡号"),
            HStack(8,
                TextBox(draft.CardExpMonth ?? string.Empty, value => update(draft with { CardExpMonth = value }), header: "月份")
                    .Flex(grow: 1, basis: 0).AutomationName("月份"),
                TextBox(draft.CardExpYear ?? string.Empty, value => update(draft with { CardExpYear = value }), header: "年份")
                    .Flex(grow: 1, basis: 0).AutomationName("年份")),
            PasswordBox(draft.CardCode ?? string.Empty, value => update(draft with { CardCode = value }), "CVV")
                .Header("CVV").AutomationName("CVV")
        ]);

    private static Element RenderIdentity(VaultItemDraft draft, Action<VaultItemDraft> update) =>
        EditorSection("身份信息",
        [
            HStack(8,
                TextBox(draft.FirstName ?? string.Empty, value => update(draft with { FirstName = value }), header: "名")
                    .Flex(grow: 1, basis: 0).AutomationName("名"),
                TextBox(draft.LastName ?? string.Empty, value => update(draft with { LastName = value }), header: "姓")
                    .Flex(grow: 1, basis: 0).AutomationName("姓")),
            TextBox(draft.Email ?? string.Empty, value => update(draft with { Email = value }), header: "邮箱").AutomationName("邮箱"),
            TextBox(draft.Phone ?? string.Empty, value => update(draft with { Phone = value }), header: "电话").AutomationName("电话"),
            TextBox(draft.Company ?? string.Empty, value => update(draft with { Company = value }), header: "公司").AutomationName("公司"),
            TextBox(draft.Address ?? string.Empty, value => update(draft with { Address = value }), header: "地址").AutomationName("地址")
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
