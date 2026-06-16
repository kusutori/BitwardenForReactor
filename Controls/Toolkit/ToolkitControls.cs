using System;
using System.Linq;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Core.V1Protocol;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI = Microsoft.UI.Xaml.Controls;

namespace BitwardenForReactor.Controls.Toolkit;

public sealed record ToolkitSettingsCardElement(
    string Header,
    string Description,
    Element? Content = null,
    string? HeaderIconGlyph = null) : Element
{
    internal Action<SettingsCard>[] Setters { get; init; } = [];
}

public sealed record ToolkitSettingsExpanderElement(
    string Header,
    string Description,
    Element? Content = null,
    Element[]? Items = null,
    string? HeaderIconGlyph = null,
    bool IsExpanded = false) : Element
{
    internal Action<SettingsExpander>[] Setters { get; init; } = [];
}

public sealed record ToolkitSegmentedElement(
    string[] Items,
    int SelectedIndex,
    Action<int>? OnSelectedIndexChanged = null) : Element
{
    internal Action<Segmented>[] Setters { get; init; } = [];
}

public static class ToolkitFactories
{
    static ToolkitFactories()
    {
        ControlRegistry.Register<ToolkitSettingsCardElement, SettingsCard>(
            static () => new ToolkitSettingsCardHandler());
        ControlRegistry.RegisterDecorator<ToolkitSettingsExpanderElement>(
            static () => new ToolkitSettingsExpanderHandler());
        ControlRegistry.Register<ToolkitSegmentedElement, Segmented>(
            static () => new ToolkitSegmentedHandler());
    }

    public static ToolkitSettingsCardElement SettingsCard(
        string header,
        string description,
        Element? content = null,
        string? headerIconGlyph = null) =>
        new(header, description, content, headerIconGlyph);

    public static ToolkitSettingsExpanderElement SettingsExpander(
        string header,
        string description,
        Element? content = null,
        Element[]? items = null,
        string? headerIconGlyph = null,
        bool isExpanded = false) =>
        new(header, description, content, items, headerIconGlyph, isExpanded);

    public static ToolkitSegmentedElement Segmented(
        string[] items,
        int selectedIndex,
        Action<int>? onSelectedIndexChanged = null) =>
        new(items, selectedIndex, onSelectedIndexChanged);
}

internal sealed class ToolkitSettingsCardHandler : IElementHandler<ToolkitSettingsCardElement, SettingsCard>
{
    private static readonly SingleContent<ToolkitSettingsCardElement, SettingsCard> ChildrenStrategy =
        new(
            GetChild: element => element.Content,
            SetChild: (control, child) => control.Content = child)
        {
            GetCurrentChild = control => control.Content as UIElement,
        };

    public SettingsCard Mount(MountContext ctx, ToolkitSettingsCardElement element)
    {
        var control = ctx.RentControl<SettingsCard>();
        Apply(control, element);
        ctx.ApplySetters(element.Setters, control);
        return control;
    }

    public void Update(UpdateContext ctx, ToolkitSettingsCardElement oldElement, ToolkitSettingsCardElement newElement, SettingsCard control)
    {
        Apply(control, newElement);
        ctx.ApplySetters(newElement.Setters, control);
    }

    public ChildrenStrategy<ToolkitSettingsCardElement, SettingsCard>? Children => ChildrenStrategy;

    private static void Apply(SettingsCard control, ToolkitSettingsCardElement element)
    {
        control.Header = element.Header;
        control.Description = element.Description;
        control.HeaderIcon = CreateIcon(element.HeaderIconGlyph)!;
        control.IsClickEnabled = false;
        control.IsActionIconVisible = false;
    }

    private static WinUI.IconElement? CreateIcon(string? glyph) =>
        string.IsNullOrWhiteSpace(glyph) ? null : new FontIcon { Glyph = glyph };
}

internal sealed class ToolkitSettingsExpanderHandler : IDecoratorElementHandler<ToolkitSettingsExpanderElement>
{
    public UIElement Mount(MountContext ctx, ToolkitSettingsExpanderElement element)
    {
        var control = new SettingsExpander();
        Apply(control, element);
        control.Content = MountOptional(ctx, element.Content)!;
        RebuildItems(ctx, control, element);
        Reconciler.SetElementTag(control, element);
        Reconciler.ApplySetters(element.Setters, control);
        return control;
    }

    public UIElement Update(UpdateContext ctx, ToolkitSettingsExpanderElement oldElement, ToolkitSettingsExpanderElement newElement, UIElement control)
    {
        var expander = (SettingsExpander)control;
        Apply(expander, newElement);
        ReconcileContent(ctx, expander, oldElement.Content, newElement.Content);
        RebuildItems(ctx, expander, newElement);
        Reconciler.SetElementTag(expander, newElement);
        Reconciler.ApplySetters(newElement.Setters, expander);
        return expander;
    }

    public V1UnmountDisposition Unmount(UnmountContext ctx, ToolkitSettingsExpanderElement? element, UIElement control) =>
        V1UnmountDisposition.ContinueDefaultTraversal;

    private static void Apply(SettingsExpander control, ToolkitSettingsExpanderElement element)
    {
        control.Header = element.Header;
        control.Description = element.Description;
        control.HeaderIcon = (string.IsNullOrWhiteSpace(element.HeaderIconGlyph)
            ? null
            : new FontIcon { Glyph = element.HeaderIconGlyph })!;
        control.IsExpanded = element.IsExpanded;
    }

    private static UIElement? MountOptional(MountContext ctx, Element? content) =>
        content is null ? null : ctx.Reconciler.Mount(content, ctx.RequestRerender);

    private static void ReconcileContent(UpdateContext ctx, SettingsExpander control, Element? oldContent, Element? newContent)
    {
        if (newContent is null)
        {
            if (control.Content is UIElement removedChild)
            {
                ctx.Reconciler.UnmountChild(removedChild);
            }

            control.Content = null!;
            return;
        }

        if (Equals(oldContent, newContent))
        {
            return;
        }

        if (control.Content is UIElement oldChild)
        {
            ctx.Reconciler.UnmountChild(oldChild);
        }

        control.Content = ctx.Reconciler.Mount(newContent, ctx.RequestRerender)!;
    }

    private static void RebuildItems(MountContext ctx, SettingsExpander control, ToolkitSettingsExpanderElement element)
    {
        control.Items.Clear();
        foreach (var item in element.Items ?? [])
        {
            if (ctx.Reconciler.Mount(item, ctx.RequestRerender) is { } mounted)
            {
                control.Items.Add(mounted);
            }
        }
    }

    private static void RebuildItems(UpdateContext ctx, SettingsExpander control, ToolkitSettingsExpanderElement element)
    {
        foreach (var item in control.Items.OfType<UIElement>().ToArray())
        {
            ctx.Reconciler.UnmountChild(item);
        }

        control.Items.Clear();
        foreach (var item in element.Items ?? [])
        {
            if (ctx.Reconciler.Mount(item, ctx.RequestRerender) is { } mounted)
            {
                control.Items.Add(mounted);
            }
        }
    }
}

internal sealed class ToolkitSegmentedHandler : IElementHandler<ToolkitSegmentedElement, Segmented>
{
    public Segmented Mount(MountContext ctx, ToolkitSegmentedElement element)
    {
        var control = ctx.RentControl<Segmented>();
        Apply(control, element);
        control.SelectionChanged += OnSelectionChanged;
        ctx.ApplySetters(element.Setters, control);
        return control;
    }

    public void Update(UpdateContext ctx, ToolkitSegmentedElement oldElement, ToolkitSegmentedElement newElement, Segmented control)
    {
        Apply(control, newElement);
        ctx.ApplySetters(newElement.Setters, control);
    }

    public void Unmount(UnmountContext ctx, Segmented control)
    {
        control.SelectionChanged -= OnSelectionChanged;
        control.Items.Clear();
    }

    private static void Apply(Segmented control, ToolkitSegmentedElement element)
    {
        if (control.Items.Count != element.Items.Length ||
            !control.Items.OfType<SegmentedItem>().Select(item => item.Content?.ToString()).SequenceEqual(element.Items))
        {
            control.Items.Clear();
            foreach (var item in element.Items)
            {
                control.Items.Add(new SegmentedItem { Content = item });
            }
        }

        if (element.SelectedIndex >= 0 && element.SelectedIndex < control.Items.Count && control.SelectedIndex != element.SelectedIndex)
        {
            control.SelectedIndex = element.SelectedIndex;
        }

        Reconciler.SetElementTag(control, element);
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Segmented control &&
            Reconciler.GetElementTag(control) is ToolkitSegmentedElement element)
        {
            element.OnSelectedIndexChanged?.Invoke(control.SelectedIndex);
        }
    }
}
