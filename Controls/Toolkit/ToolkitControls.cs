using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor.Wrappers;

namespace BitwardenForReactor.Controls.Toolkit;

[GenerateReactorWrapper(typeof(SettingsCard), Exclude = ["CommandParameter"])]
[WrapElementSlot("HeaderIcon")]
public partial record SettingsCardElement;

[GenerateReactorWrapper(typeof(SettingsExpander))]
[WrapElementSlot("HeaderIcon")]
public partial record SettingsExpanderElement;

[GenerateReactorWrapper(typeof(Segmented))]
[WrapControlled("SelectedIndex", ChangedEvent = "SelectionChanged")]
public partial record SegmentedElement;
