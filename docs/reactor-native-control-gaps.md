# Reactor 原生控件回退清单

最后审计日期：2026-06-20

当前依赖版本：

- `Microsoft.UI.Reactor 0.0.0-local`（来自同级 `microsoft-ui-reactor` 源码仓库）
- `CommunityToolkit.WinUI.Controls.SettingsControls 8.2.251219`
- `CommunityToolkit.WinUI.Controls.Segmented 8.2.251219`

本文记录项目中因 Reactor 尚未封装控件或未暴露所需属性，而直接访问原生 WinUI/Toolkit 控件的代码。升级 Reactor 后应优先检查本清单，并在官方 API 已覆盖时删除对应回退。

## 状态说明

- **必须回退**：当前 Reactor API 无法表达需求，暂时必须使用原生控件或 `.Set(...)`。
- **自定义适配**：Reactor 没有该第三方控件，需要项目自行实现 Element 和 Handler。
- **可立即清理**：Reactor 已提供正式 API，现有原生写法不再必要，不应作为长期兼容代码保留。
- **观察项**：当前没有启用原生回退，但轻量数据模型仍缺少部分 WinUI 属性。

## 必须回退

### RNC-001：MenuFlyoutItem 的前景色

| 项目 | 内容 |
| --- | --- |
| Reactor 类型 | `MenuFlyoutItemData` / `MenuItem(...)` |
| 缺失能力 | `Foreground` 或 `Style` 属性映射 |
| 使用场景 | “更多”菜单中的“删除”项需要使用系统危险色 |
| 当前方案 | 创建原生 `MenuFlyout` 和 `MenuFlyoutItem`，为删除项设置 `Foreground` |
| 代码位置 | [`Components/VaultListItem.cs`](../Components/VaultListItem.cs) 的 `BuildMoreFlyout`、`NativeMenuItem` |
| 移除条件 | Reactor 的 `MenuFlyoutItemData` 同时在创建和更新路径中映射 `Foreground`/`Style` |
| 状态 | **必须回退** |

当前 `MenuFlyoutItemData` 只提供 `Text`、`OnClick`、`Icon`、`IsEnabled`、`IconElement`、`KeyboardAccelerators`、`AccessKey` 和 `Description`。因此不能直接写出类似下面的声明式代码：

```csharp
MenuItem("删除", onDelete, icon: "\uE74D")
    .Foreground(Theme.SystemCritical)
```

升级后不能只检查类型上是否新增了属性，还要确认 `MenuCommandFactory` 或对应 handler 的创建、更新逻辑都会把属性同步到原生 `MenuFlyoutItem`。

### RNC-002：Button 内容对齐

| 项目 | 内容 |
| --- | --- |
| Reactor 类型 | `ButtonElement` |
| 缺失能力 | `HorizontalContentAlignment`、`VerticalContentAlignment` 专用 modifier |
| 使用场景 | 密码库列表的紧凑图标按钮需要内容稳定居中 |
| 当前方案 | `.Set(button => ...)` 直接设置原生 `Button` 属性 |
| 代码位置 | [`Components/VaultListItem.cs`](../Components/VaultListItem.cs) 的 `CompactIconButton` |
| 移除条件 | Reactor 为内容对齐提供声明式属性或 modifier |
| 状态 | **必须回退** |

`MinWidth`、`MinHeight` 和 `Padding` 已经由通用 Reactor modifier 支持，不属于此缺口。后续整理时可把它们移出 `.Set(...)`，只保留尚未封装的内容对齐设置。

## 第三方控件适配

Reactor 当前没有为以下 Windows Community Toolkit 控件提供官方 Element。这类代码不是单个属性缺失，而是整个控件需要项目自行接入。

| 控件 | 项目 Element | 已映射能力 | 代码位置 | 状态 |
| --- | --- | --- | --- | --- |
| `SettingsCard` | `ToolkitSettingsCardElement` | Header、Description、Content、HeaderIconGlyph | [`Controls/Toolkit/ToolkitControls.cs`](../Controls/Toolkit/ToolkitControls.cs) | **自定义适配** |
| `SettingsExpander` | `ToolkitSettingsExpanderElement` | Header、Description、Content、Items、HeaderIconGlyph、IsExpanded | [`Controls/Toolkit/ToolkitControls.cs`](../Controls/Toolkit/ToolkitControls.cs) | **自定义适配** |
| `Segmented` | `ToolkitSegmentedElement` | Items、SelectedIndex、选择回调 | [`Controls/Toolkit/ToolkitControls.cs`](../Controls/Toolkit/ToolkitControls.cs) | **自定义适配** |

相关 Toolkit 程序集在 [`Program.cs`](../Program.cs) 中通过 `ReactorApp.RegisterControlAssembly(...)` 注册，以便 WinUI 加载控件资源。

升级 Reactor 或新增官方 Toolkit 集成包后，应比较官方 wrapper 的属性覆盖和更新语义，再决定是否替换。不要仅因为官方出现同名 Element 就立即删除本地适配器。

## 可立即清理

当前没有已知的可立即清理项。

`button.Flyout = BuildMoreFlyout(...)` 仍需保留：虽然 Reactor 提供 `.WithFlyout(...)`，但它接收 Reactor `Element`，不能包装当前为实现危险色而创建的原生 `MenuFlyout`。应与 RNC-001 一并迁移。

## 观察项

### NavigationViewItemData

当前树状导航通过 `NavigationViewItemData.Children` 实现，未使用原生控件回退。但该轻量数据类型目前没有暴露 `IsExpanded`、`Style`、`ToolTip` 等完整的 `NavigationViewItem` 属性。

项目当前不需要强制控制展开状态，因此不添加原生实现。若将来需要由状态驱动展开/折叠，应先检查新版 Reactor 是否已补充对应映射，再考虑本地扩展。

## 升级审计流程

每次升级 `Microsoft.UI.Reactor` 后执行以下检查：

1. 查看新版 `reactor.api.txt`，逐项搜索本清单中的控件和属性。
2. 检查官方实现是否同时覆盖原生控件的创建和后续更新，而不只是增加 record 属性。
3. 搜索项目中的原生逃生口：

```powershell
rg -n "\.Set\(|new WinUI\.|ControlRegistry\.Register|RegisterControlAssembly" .
```

4. 将已经有正式 API 的代码改回 Reactor DSL，并验证交互和主题切换。
5. 执行构建和对应界面的手工测试。
6. 更新本文的版本、日期和状态；已解决项移入“已解决记录”。
7. 完成这一轮升级适配后单独提交 Git，避免与功能开发混在同一个提交中。

## 适配原则

1. Reactor 已提供属性且具备更新语义时，优先使用 Reactor DSL。
2. 只缺少一个孤立属性时，优先使用 `.Set(...)`，并在本清单登记。
3. 控件被多处复用或涉及事件订阅、子项更新时，使用自定义 Element 和 Handler。
4. 轻量投影类型无法表达样式或状态时，才整体退回原生控件。
5. 每完成一个独立的大环节都提交 Git，保持提交范围可审查、可回退。

## 已解决记录

| 日期 | 清理内容 | 替代 API |
| --- | --- | --- |
| 2026-06-20 | `ProgressRing.IsActive` 原生设置 | `.IsActive()` |
| 2026-06-20 | `TextBox.AcceptsReturn` 和 `MinHeight` 原生设置 | `.AcceptsReturn()`、`.MinHeight(...)` |
| 2026-06-20 | 紧凑按钮的 `MinWidth`、`MinHeight` 和 `Padding` 原生设置 | 通用尺寸和 `.Padding(...)` modifier |
