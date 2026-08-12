using System;
using System.Collections.Generic;
using System.Linq;
using BitwardenForReactor.Application;
using BitwardenForReactor.Models;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hooks;
using Microsoft.UI.Reactor.Input;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using static BitwardenForReactor.Controls.Toolkit.SegmentedElement;
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
            .AutomationName(T("Item editor overlay"));
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

        var typeNames = new[] { T("Logins"), T("Secure notes"), T("Cards"), T("Identities") };
        var typeValues = new[] { BitwardenItemType.Login, BitwardenItemType.SecureNote, BitwardenItemType.Card, BitwardenItemType.Identity };
        var selectedType = Math.Max(0, Array.IndexOf(typeValues, draft.Type));
        var folderNames = new[] { T("No folder") }.Concat(Props.Folders.Select(folder => folder.Name)).ToArray();
        var selectedFolder = string.IsNullOrWhiteSpace(draft.FolderId)
            ? 0
            : Math.Max(0, Props.Folders.ToList().FindIndex(folder => folder.Id == draft.FolderId) + 1);

        var form = ScrollView(
                VStack(16,
                EditorSection(T("Basic information"),
                [
                    VStack(6,
                        TextBlock(T("Type")).Foreground(Theme.SecondaryText),
                        Segmented(items: typeNames, selectedIndex: selectedType, onSelectedIndexChanged: index =>
                        {
                            Update(current => current with
                            {
                                Type = typeValues[Math.Clamp(index, 0, typeValues.Length - 1)]
                            });
                            formScroll.Current?.ScrollTo(0, 0);
                        })
                            .AutomationName(T("Item type"))),
                    TextBox(draft.Name, value => Update(current => current with { Name = value }), header: T("Name"))
                        .AutomationName(T("Name")),
                    ComboBox(folderNames, selectedFolder, index =>
                            Update(current => current with
                            {
                                FolderId = index <= 0 || index > Props.Folders.Count
                                    ? null
                                    : Props.Folders[index - 1].Id
                            }))
                        .Header(T("Folders"))
                        .HorizontalAlignment(HorizontalAlignment.Stretch)
                        .AutomationName(T("Folders")),
                    string.IsNullOrWhiteSpace(draft.Name)
                        ? TextBlock(T("Name is required.")).Foreground(Theme.SystemCaution)
                        : null
                ]),
                draft.Type == BitwardenItemType.Login ? RenderLogin(draft, Update) : null,
                draft.Type == BitwardenItemType.Card ? RenderCard(draft, Update) : null,
                draft.Type == BitwardenItemType.Identity ? RenderIdentity(draft, Update) : null,
                draft.Type == BitwardenItemType.SecureNote ? RenderSecureNote() : null,
                EditorSection(T("Notes and options"),
                [
                    TextBox(draft.Notes ?? string.Empty, value => Update(current => current with { Notes = value }), header: T("Notes"))
                        .TextWrapping()
                        .AcceptsReturn()
                        .MinHeight(100)
                        .AutomationName(T("Notes")),
                    CheckBox(draft.Favorite, value => Update(current => current with { Favorite = value }), T("Favorite"))
                        .AutomationName(T("Favorite"))
                ])))
            .Padding(24)
            .Ref(formScroll)
            .Grid(row: 1);

        return Border(
                Grid(
                    columns: [GridSize.Star()],
                    rows: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                    Heading(draft.Id is null ? T("New item") : T("Edit item"))
                        .Margin(left: 24, top: 20, right: 24, bottom: 12)
                        .Grid(row: 0),
                    form,
                    Border(
                            HStack(12,
                                Button(T("Save"), () => Props.OnSave(draft))
                                    .MinWidth(96)
                                    .IsEnabled(!string.IsNullOrWhiteSpace(draft.Name))
                                    .AutomationName(T("Save item")),
                                Button(T("Cancel"), Props.OnCancel)
                                    .MinWidth(96)
                                    .AutomationName(T("Cancel editing")))
                                .HorizontalAlignment(HorizontalAlignment.Left))
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
            .AutomationName(T("Item editor"));
    }

    private static Element RenderLogin(VaultItemDraft draft, Action<Func<VaultItemDraft, VaultItemDraft>> update) =>
        EditorSection(T("Login information"),
        [
            TextBox(draft.Username ?? string.Empty, value => update(current => current with { Username = value }), header: T("Username")).AutomationName(T("Username")),
            PasswordBox(draft.Password ?? string.Empty, value => update(current => current with { Password = value }), T("Password"))
                .Header(T("Password"))
                .Set(passwordBox => passwordBox.PasswordRevealMode = PasswordRevealMode.Peek)
                .AutomationName(T("Password")),
            Component<UriEditorList, UriEditorListProps>(new UriEditorListProps(
                draft.Uris,
                uris => update(current => current with { Uris = uris })))
        ]);

    private static Element RenderCard(VaultItemDraft draft, Action<Func<VaultItemDraft, VaultItemDraft>> update) =>
        EditorSection(T("Card information"),
        [
            TextBox(draft.CardBrand ?? string.Empty, value => update(current => current with { CardBrand = value }), header: T("Brand")).AutomationName(T("Brand")),
            TextBox(draft.CardholderName ?? string.Empty, value => update(current => current with { CardholderName = value }), header: T("Cardholder")).AutomationName(T("Cardholder")),
            PasswordBox(draft.CardNumber ?? string.Empty, value => update(current => current with { CardNumber = value }), T("Card number"))
                .Header(T("Card number"))
                .Set(passwordBox => passwordBox.PasswordRevealMode = PasswordRevealMode.Peek)
                .AutomationName(T("Card number")),
            HStack(8,
                TextBox(draft.CardExpMonth ?? string.Empty, value => update(current => current with { CardExpMonth = value }), header: T("Month"))
                    .Flex(grow: 1, basis: 0).AutomationName(T("Month")),
                TextBox(draft.CardExpYear ?? string.Empty, value => update(current => current with { CardExpYear = value }), header: T("Year"))
                    .Flex(grow: 1, basis: 0).AutomationName(T("Year"))),
            PasswordBox(draft.CardCode ?? string.Empty, value => update(current => current with { CardCode = value }), "CVV")
                .Header("CVV")
                .Set(passwordBox => passwordBox.PasswordRevealMode = PasswordRevealMode.Peek)
                .AutomationName("CVV")
        ]);

    private static Element RenderIdentity(VaultItemDraft draft, Action<Func<VaultItemDraft, VaultItemDraft>> update) =>
        EditorSection(T("Identity information"),
        [
            HStack(8,
                TextBox(draft.FirstName ?? string.Empty, value => update(current => current with { FirstName = value }), header: T("First name"))
                    .Flex(grow: 1, basis: 0).AutomationName(T("First name")),
                TextBox(draft.LastName ?? string.Empty, value => update(current => current with { LastName = value }), header: T("Last name"))
                    .Flex(grow: 1, basis: 0).AutomationName(T("Last name"))),
            TextBox(draft.Email ?? string.Empty, value => update(current => current with { Email = value }), header: T("Email")).AutomationName(T("Email")),
            TextBox(draft.Phone ?? string.Empty, value => update(current => current with { Phone = value }), header: T("Phone")).AutomationName(T("Phone")),
            TextBox(draft.Company ?? string.Empty, value => update(current => current with { Company = value }), header: T("Company")).AutomationName(T("Company")),
            TextBox(draft.Address ?? string.Empty, value => update(current => current with { Address = value }), header: T("Address")).AutomationName(T("Address"))
        ]);

    private static Element RenderSecureNote() =>
        EditorSection(T("Secure notes"),
        [
            TextBlock(T("Secure notes only require a name and notes."))
                .Foreground(Theme.SecondaryText)
                .TextWrapping()
        ]);

    private static Element EditorSection(string title, IReadOnlyList<Element?> children) =>
        VStack(8,
            TextBlock(title).SemiBold(),
            VStack(10, children.Where(child => child is not null).Cast<Element>().ToArray()));
}

internal sealed record UriEditorListProps(
    IReadOnlyList<VaultUriDraft> Uris,
    Action<IReadOnlyList<VaultUriDraft>> OnChanged);

internal sealed class UriEditorList : Component<UriEditorListProps>
{
    public override Element Render()
    {
        var (draggingKey, setDraggingKey) = UseState<Guid?>(null);
        var (hoverKey, setHoverKey) = UseState<Guid?>(null);
        var (focusedKey, setFocusedKey) = UseState(Props.Uris.FirstOrDefault()?.Key ?? Guid.Empty);

        IReadOnlyList<VaultUriDraft> Replace(Guid key, Func<VaultUriDraft, VaultUriDraft> change) =>
            Props.Uris.Select(uri => uri.Key == key ? change(uri) : uri).ToArray();

        IReadOnlyList<VaultUriDraft> Remove(Guid key) =>
            Props.Uris.Count <= 1
                ? Props.Uris
                : Props.Uris.Where(uri => uri.Key != key).ToArray();

        IReadOnlyList<VaultUriDraft> Move(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return Props.Uris;
            if (fromIndex < 0 || fromIndex >= Props.Uris.Count) return Props.Uris;

            toIndex = Math.Clamp(toIndex, 0, Props.Uris.Count - 1);
            var copy = Props.Uris.ToList();
            var item = copy[fromIndex];
            copy.RemoveAt(fromIndex);
            copy.Insert(toIndex, item);
            return copy;
        }

        void MoveByKey(Guid sourceKey, Guid targetKey)
        {
            var from = Props.Uris.ToList().FindIndex(uri => uri.Key == sourceKey);
            var to = Props.Uris.ToList().FindIndex(uri => uri.Key == targetKey);
            if (from >= 0 && to >= 0)
            {
                Props.OnChanged(Move(from, to));
            }
        }

        void MoveFocused(Guid key, KeyRoutedEventArgs e)
        {
            var alt = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down) != 0;
            if (!alt) return;

            var index = Props.Uris.ToList().FindIndex(uri => uri.Key == key);
            if (index < 0) return;

            if (e.Key == VirtualKey.Up && index > 0)
            {
                Props.OnChanged(Move(index, index - 1));
                setFocusedKey(key);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down && index < Props.Uris.Count - 1)
            {
                Props.OnChanged(Move(index, index + 1));
                setFocusedKey(key);
                e.Handled = true;
            }
        }

        Element Row(VaultUriDraft uri)
        {
            var isDragging = draggingKey == uri.Key;
            var isHover = hoverKey == uri.Key && draggingKey is not null && draggingKey != uri.Key;
            var isFocused = focusedKey == uri.Key;

            return Border(
                    Grid(
                        columns: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                        rows: [GridSize.Auto],
                        Border(Icon(FontIcon("\uE700")))
                            .Width(32)
                            .Height(32)
                            .Margin(top: 24, right: 8)
                            .CornerRadius(4)
                            .Background(Theme.SubtleFill)
                            .AutomationName(T("Drag website"))
                            .OnDragStart<BorderElement, Guid>(
                                getPayload: () =>
                                {
                                    setDraggingKey(uri.Key);
                                    return uri.Key;
                                },
                                allowedOperations: DragOperations.Move,
                                onEnd: _ =>
                                {
                                    setDraggingKey(null);
                                    setHoverKey(null);
                                })
                            .Grid(column: 0),
                        TextBox(uri.Value, value => Props.OnChanged(Replace(uri.Key, current => current with { Value = value })), header: T("Website (URI)"))
                            .AutomationName(T("Website URI"))
                            .Grid(column: 1),
                        Button(Icon(FontIcon("\uE74D")), () => Props.OnChanged(Remove(uri.Key)))
                            .IsEnabled(Props.Uris.Count > 1)
                            .AutomationName(T("Remove website"))
                            .Margin(left: 8, top: 24, right: 0, bottom: 0)
                            .Grid(column: 2)))
                .WithBorder(isHover ? Theme.Accent : Theme.CardStroke, isHover ? 2 : isFocused ? 1 : 0)
                .CornerRadius(6)
                .Opacity(isDragging ? 0.55 : 1.0)
                .IsTabStop(true)
                .OnGotFocus((_, _) => setFocusedKey(uri.Key))
                .OnKeyDown((_, e) => MoveFocused(uri.Key, e))
                .OnDragEnter(args =>
                {
                    if (args.Data.TryGetTypedPayload<Guid>(out var sourceKey) && sourceKey != uri.Key)
                    {
                        setHoverKey(uri.Key);
                    }
                })
                .OnDrop<BorderElement, Guid>(sourceKey =>
                {
                    MoveByKey(sourceKey, uri.Key);
                    setDraggingKey(null);
                    setHoverKey(null);
                }, acceptedOps: DragOperations.Move)
                .WithKey(uri.Key.ToString("D"));
        }

        return VStack(8,
            Props.Uris.Select(Row)
                .Append(
                    Button(T("+  Add website"), () => Props.OnChanged([.. Props.Uris, VaultUriDraft.New()]))
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .AutomationName(T("Add website")))
                .ToArray());
    }
}
