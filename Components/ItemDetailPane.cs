using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BitwardenForReactor.Application;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Components;

public sealed class ItemDetailPane : Component<VaultPaneProps>
{
    public override Element Render()
    {
        var state = Props.State;
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

        return ScrollView(
                VStack(14,
                    Component<DetailHeader, DetailHeaderProps>(
                        new DetailHeaderProps(
                            item,
                            state.Filter == VaultFilter.Trash,
                            () => Props.Dispatch(new EditorOpened(VaultItemDraft.FromItem(item))),
                            () => Props.Dispatch(new DeleteRequested(item, false)),
                            () => Props.Dispatch(new DeleteRequested(item, true)),
                            () => _ = AppCommands.RestoreAsync(item, Props.Dispatch))),
                        RenderItemFields(item),
                    string.IsNullOrWhiteSpace(item.Notes) ? null : RenderNotes(item.Notes),
                    RenderMetadata(item))
                .MaxWidth(760))
                .Padding(20)
                .Flex(grow: 1, basis: 0)
            .Background(Theme.LayerFill)
            .Flex(grow: 1, basis: 0);
    }

    private Element RenderItemFields(BitwardenItem item)
    {
        var sections = new List<Element>();
        var primaryFields = new List<Element>();
        Action<string> copyRequested = value => { _ = AppCommands.CopyAsync(value, Props.Dispatch); };

        switch (item.Type)
        {
            case BitwardenItemType.Login:
                AddField(primaryFields, "用户名", item.Login?.Username, copyRequested);
                AddSensitiveField(primaryFields, "密码", VaultDisplay.Mask("password"), item.Login?.Password, copyRequested);
                foreach (var uri in item.Login?.Uris ?? []) AddField(primaryFields, "网站", uri.Uri, copyRequested);
                if (!string.IsNullOrWhiteSpace(item.Login?.Totp))
                {
                    primaryFields.Add(Component<TotpField, TotpFieldProps>(
                        new TotpFieldProps(() => _ = AppCommands.CopyTotpAsync(item, Props.Dispatch))));
                }
                sections.Add(DetailSection("登录信息", primaryFields));
                break;
            case BitwardenItemType.Card:
                AddField(primaryFields, "品牌", item.Card?.Brand, copyRequested);
                AddField(primaryFields, "持卡人", item.Card?.CardholderName, copyRequested);
                AddSensitiveField(primaryFields, "卡号", VaultDisplay.MaskCard(item.Card?.Number), item.Card?.Number, copyRequested);
                AddField(primaryFields, "有效期", VaultDisplay.FormatExpiry(item.Card), copyRequested);
                AddSensitiveField(primaryFields, "CVV", VaultDisplay.Mask("cvv"), item.Card?.Code, copyRequested);
                sections.Add(DetailSection("卡片信息", primaryFields));
                break;
            case BitwardenItemType.Identity:
                AddField(primaryFields, "姓名", VaultDisplay.JoinParts(item.Identity?.FirstName, item.Identity?.LastName), copyRequested);
                AddField(primaryFields, "邮箱", item.Identity?.Email, copyRequested);
                AddField(primaryFields, "电话", item.Identity?.Phone, copyRequested);
                AddField(primaryFields, "公司", item.Identity?.Company, copyRequested);
                AddField(primaryFields, "地址", VaultDisplay.JoinParts(item.Identity?.Address1, item.Identity?.Address2, item.Identity?.Address3, item.Identity?.City, item.Identity?.State, item.Identity?.PostalCode, item.Identity?.Country), copyRequested);
                AddSensitiveField(primaryFields, "SSN", VaultDisplay.Mask("ssn"), item.Identity?.Ssn, copyRequested);
                AddField(primaryFields, "护照", item.Identity?.PassportNumber, copyRequested);
                AddField(primaryFields, "驾照", item.Identity?.LicenseNumber, copyRequested);
                sections.Add(DetailSection("身份信息", primaryFields));
                break;
            case BitwardenItemType.SecureNote:
                break;
        }

        if (item.Fields?.Count > 0)
        {
            sections.Add(DetailSection("自定义字段", item.Fields.Select(field =>
                    field.Type == CustomFieldType.Hidden
                        ? Component<SensitiveField, SensitiveFieldProps>(
                            new SensitiveFieldProps(field.Name, VaultDisplay.Mask("hidden"), field.Value, copyRequested))
                        : DetailField(field.Name, field.Value ?? string.Empty, field.Value, copyRequested))
                .Select((element, index) => element.WithKey($"{item.Id}:field:{index}"))
                .ToArray()));
        }

        return VStack(14, sections.ToArray());
    }

    private static void AddField(List<Element> fields, string label, string? value, Action<string> copyRequested, string? copyValue = null)
    {
        if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(copyValue)) return;
        fields.Add(DetailField(label, value ?? string.Empty, copyValue ?? value, copyRequested));
    }

    private static void AddSensitiveField(List<Element> fields, string label, string? maskedValue, string? value, Action<string> copyRequested)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        fields.Add(Component<SensitiveField, SensitiveFieldProps>(
            new SensitiveFieldProps(label, maskedValue ?? VaultDisplay.Mask("hidden"), value, copyRequested)));
    }

    private static Element DetailSection(string title, IReadOnlyList<Element> children) =>
        Component<DetailSection, DetailSectionProps>(new DetailSectionProps(title, children));

    private static Element DetailField(string label, string value, string? copyValue, Action<string> copyRequested) =>
        Component<DetailFieldRow, DetailFieldRowProps>(new DetailFieldRowProps(label, value, copyValue, copyRequested));

    private Element RenderNotes(string notes) =>
        DetailSection("备注",
        [
            DetailField("内容", notes, notes, value => { _ = AppCommands.CopyAsync(value, Props.Dispatch); })
        ]);

    private Element RenderMetadata(BitwardenItem item)
    {
        Action<string> copyRequested = value => { _ = AppCommands.CopyAsync(value, Props.Dispatch); };
        return DetailSection("项目记录",
        [
            DetailField("修改时间", item.RevisionDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture), null, copyRequested),
            DetailField("创建时间", item.CreationDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture), null, copyRequested),
            DetailField("项目 ID", item.Id, item.Id, copyRequested)
        ]);
    }
}
