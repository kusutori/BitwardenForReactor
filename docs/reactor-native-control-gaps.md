# Reactor 原生控件回退清单

最后审计日期：2026-06-26

当前依赖版本：

- `Microsoft.UI.Reactor 0.1.0-preview.11`
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

### RNC-003：NavigationViewItem 的自定义内容和右侧操作

| 项目 | 内容 |
| --- | --- |
| Reactor 类型 | `NavigationViewItemData` |
| 缺失能力 | `Content` 只能传入 `string`，无法声明式传入自定义 `UIElement` 或右侧操作区域；也无法绑定 `IsExpanded` 派生的图标状态 |
| 使用场景 | 左侧导航“文件夹”子项需要在文本右侧显示编辑按钮；“文件夹”父项展开时需要切换图标 |
| 当前方案 | 在 `NavigationView` 创建后通过 `.Set(...)` 遍历原生 `NavigationViewItem`，把匹配文件夹项的 `Content` 替换为原生 `Grid + TextBlock + Button`；对 `Folders` 项监听原生 `IsExpandedProperty` 并替换 `Icon` |
| 代码位置 | [`Shell/BitwardenShell.cs`](../Shell/BitwardenShell.cs) 的 `AttachFolderEditButtons`、`AttachExpandableFolderIcon`、`FolderNavContent` |
| 移除条件 | Reactor 的导航数据模型支持 `Element`/`UIElement` 内容模板，或提供可组合的 item content / trailing action API，并且支持由 `IsExpanded` 或等价状态驱动 item icon；更新路径不会覆盖自定义内容和图标 |
| 状态 | **必须回退** |

当前 `NavigationViewItemData` 适合表达普通树状导航：文本、图标、Tag、Children、Separator、Header、Disabled、KeyboardAccelerators、AccessKey 和 Description。但它不能表达“左侧文本 + 右侧按钮”这种 item template，也不能表达“根据展开状态切换图标”的 item state。

项目现在仍然使用 Reactor 的 `NavigationViewItemData.Children` 构建树状结构，只在文件夹项创建完成后补一个原生内容替换。升级 Reactor 后需要重点检查以下事项：

1. 数据模型是否允许传入自定义内容或 trailing action。
2. 数据模型是否允许读取或绑定展开状态，至少能让展开项图标由 Reactor 状态驱动。
3. `NavigationView` 的更新逻辑是否会在每次 render 时把原生 `Content` 或 `Icon` 重置回字符串声明值。

如果只新增了创建时的自定义内容支持，但更新路径仍会覆盖内容，这个回退仍不能删除。

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

当前树状导航通过 `NavigationViewItemData.Children` 实现，文件夹右侧编辑按钮和展开态图标见 RNC-003。该轻量数据类型目前仍没有暴露 `IsExpanded`、`Style`、`ToolTip` 等完整的 `NavigationViewItem` 属性。

项目当前不需要强制控制展开状态，因此不添加原生实现。若将来需要由状态驱动展开/折叠，应先检查新版 Reactor 是否已补充对应映射，再考虑本地扩展。

## 已知框架/渲染问题

### BUG-001：自定义弹窗卡片阴影二次打开不稳定

| 项目 | 内容 |
| --- | --- |
| 涉及界面 | 项目编辑弹窗、文件夹编辑弹窗 |
| 现象 | 弹窗第一次打开时卡片有阴影，关闭后第二次打开阴影可能消失 |
| 已尝试方案 | 原生 `ThemeShadow + Translation.Z`；伪阴影层；两种方案都未能稳定解决 |
| 当前方案 | 不再显式设置阴影，保留当前弹窗结构，把问题记录为框架或当前自定义弹窗实现限制 |
| 代码位置 | [`Components/ItemEditorDialog.cs`](../Components/ItemEditorDialog.cs)、[`Components/FolderEditorDialog.cs`](../Components/FolderEditorDialog.cs) |
| 复查条件 | Reactor/WinUI 对 `ContentDialog`、overlay、shadow 或 visual tree 生命周期有更新后重新验证 |
| 状态 | **观察项** |

这个问题不是单纯缺少一个属性映射。项目曾经使用原生 `ThemeShadow` 和 `Translation` 尝试增强弹窗层级，但第二次打开仍然不稳定；后来又尝试确定性的伪阴影层，视觉收益有限，已回退。

短期内不要继续在业务组件里堆叠阴影补丁。后续更合适的处理路径是：

1. 等待 Reactor/WinUI 相关弹窗和 overlay 行为更新后复查。
2. 如果必须稳定实现，则抽成独立弹窗宿主或自定义控件统一处理，不在每个业务弹窗里分别 patch。
3. 重新评估是否能回到原生 `ContentDialog`，前提是动态表单内容更新问题已经解决。

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
