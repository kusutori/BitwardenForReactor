# BitwardenForReactor UI 优化方案

## 目标

当前版本已经完成 Reactor/MVU 的基础结构和核心功能，但 UI 仍偏工程原型。下一阶段目标不是重写业务逻辑，而是在保持现有状态模型和服务层稳定的前提下，提升 WinUI 原生感、信息密度和日常使用效率。

优化原则：

- 优先使用成熟 WinUI / Windows Community Toolkit 控件，并通过 Reactor adapter 接入。
- 对密码管理器特有的交互保留自定义组件，例如敏感字段、复制按钮、详情字段行。
- 不引入当前项目用不到的表格控件选型讨论。
- 暂不做动画系统和自定义导航模板，后续有明确需求再规划。
- 每完成一个大环节都提交一次 git，保持小步可回退的开发习惯。

## 控件接入策略

Reactor 渲染的仍是真实 WinUI 控件，因此可以和现有 WinUI 控件生态互操作。下一阶段建议新增 `Controls/` 目录，集中放社区控件的 Reactor adapter。

优先接入：

- `SettingsCard`
  - 用于设置页的单项配置。
  - 适合 CLI 路径、剪贴板清除时间、自动锁定时间等简单设置。
- `SettingsExpander`
  - 用于高级设置分组。
  - 适合 API Key、自定义环境变量、诊断信息等低频配置。
- `Segmented`
  - 用于轻量模式切换。
  - 可用于详情页的“概览 / 字段 / 高级”，或列表区的类型筛选补充。

仍然自定义：

- Vault 列表项模板。
- 详情页字段行。
- 敏感字段组件。
- 复制按钮与 TOTP 操作。
- 新建/编辑项目表单的领域结构。

## 布局优化

整体布局保持标准 `NavigationView + list/detail workspace`，不做自定义导航。

目标结构：

- App Shell
  - 顶部 TitleBar 只保留标题、账户状态、同步和锁定等全局动作。
  - 左侧 NavigationView 负责一级分区：全部、登录、卡片、身份、笔记、收藏、回收站、设置。
- Vault Workspace
  - 左侧列表面板：搜索、筛选、项目列表、计数。
  - 右侧详情面板：标题区、主要字段、备注、自定义字段、操作区。
- Dialog Layer
  - 新建/编辑使用分组表单。
  - 删除/永久删除继续用确认 dialog。
  - 成功/失败反馈使用非阻塞 InfoBar。

具体调整：

- 左侧列表面板宽度控制在 `360-420`，避免宽屏下列表过宽。
- 右侧详情页减少“每个字段一个卡片”的视觉噪音，改为分组字段行。
- 忙碌状态从大块区域改为更轻量的顶部状态条或局部 ProgressRing。
- 空状态根据当前筛选显示不同文案。

## Vault 列表优化

当前列表功能完整，但扫读效率和视觉层次不足。

优化方向：

- 抽出 `VaultListItem` 组件。
- 左侧使用 favicon 或类型 glyph，统一圆形尺寸。
- 主行显示项目名称和收藏状态。
- 副行显示用户名、域名或类型。
- 回收站项目显示删除时间或“已删除”状态。
- 搜索为空、收藏为空、回收站为空时显示不同空状态。
- 选中态尽量依赖 WinUI 原生 ListView visual，减少手写背景。

暂不引入额外列表/表格控件。当前密码库主界面更适合 list-detail，而不是表格视图。

## 详情页优化

详情页是当前最需要打磨的区域。目标是提高信息密度，同时让复制、查看敏感字段、编辑和删除更顺手。

建议拆分组件：

- `DetailHeader`
  - icon/favicon、名称、类型、收藏状态。
  - 主操作：复制用户名、复制密码、编辑、删除。
- `DetailSection`
  - 统一字段分组容器。
  - 分组包括登录信息、卡片信息、身份信息、自定义字段、备注。
- `DetailFieldRow`
  - label、value、右侧操作按钮。
  - 支持长文本换行和 URL 显示。
- `SensitiveField`
  - 默认掩码。
  - 复制真实值。
  - 后续可增加“短暂显示”。
- `TotpField`
  - 点击获取或刷新 TOTP。
  - 复制后显示提示。

字段展示策略：

- 登录项优先展示用户名、密码、网站、TOTP。
- 卡片优先展示卡号、有效期、CVV、持卡人。
- 身份优先展示姓名、邮箱、电话、地址。
- 自定义字段放在独立分组中。
- 备注放在底部，避免挤占主字段区域。

## 设置页优化

设置页优先使用 Windows Community Toolkit 控件，这是最容易立刻提升原生感的部分。

建议结构：

- 基础设置
  - `SettingsCard`: Bitwarden CLI 路径。
  - `SettingsCard`: 剪贴板自动清除秒数。
  - `SettingsCard`: 自动锁定分钟数。
- 认证与环境
  - `SettingsExpander`: BW_CLIENTID。
  - `SettingsExpander`: BW_CLIENTSECRET。
  - `SettingsExpander`: 自定义环境变量。
- 诊断
  - `SettingsCard`: 检测 `bw status`。
  - `SettingsCard`: 显示当前 server、账号邮箱、锁定状态。

交互要求：

- 每个设置项提供 `Header` 和 `Description`，减少 placeholder 承担说明的情况。
- 保存行为保持明确，可以继续使用“保存设置”按钮。
- 保存成功后显示 InfoBar。
- 设置变更不影响当前密码库状态，只有保存后才更新 `SettingsManager`。

## 新建与编辑表单优化

当前表单已经可用，但需要更像真实密码管理器。

建议：

- Dialog 内按类型显示不同字段分组。
- 类型选择使用 Segmented 或 ComboBox。
- 登录项字段顺序：名称、用户名、密码、网站、备注、收藏。
- 卡片字段顺序：名称、品牌、持卡人、卡号、有效期、CVV、备注、收藏。
- 身份字段顺序：名称、姓名、邮箱、电话、公司、地址、备注、收藏。
- 必填校验只做最小集合：名称必须存在。
- 保存失败时保留表单内容，不关闭 dialog。

## 实施顺序

### Phase 1: Toolkit Adapter

- 添加 Windows Community Toolkit 相关 package。
- 新增 `Controls/Toolkit/SettingsCardElement`。
- 新增 `Controls/Toolkit/SettingsExpanderElement`。
- 如实现成本合适，再接入 `SegmentedElement`。
- 构建通过后提交 git。

提交建议：

```text
Add Toolkit Reactor adapters
```

### Phase 2: 设置页重构

- 用 SettingsCard / SettingsExpander 重写设置页。
- 保持现有 `AppSettings`、`SettingsManager` 和 reducer 结构。
- 增加诊断信息卡片。
- 构建和手动验证后提交 git。

提交建议：

```text
Polish settings page with Toolkit controls
```

### Phase 3: Vault 列表重构

- 抽出 `VaultListItem`。
- 优化 icon、主副行、收藏状态、空状态。
- 调整列表面板宽度、搜索区和计数区。
- 构建和手动验证后提交 git。

提交建议：

```text
Refine vault list presentation
```

### Phase 4: 详情页重构

- 抽出 `DetailHeader`、`DetailSection`、`DetailFieldRow`、`SensitiveField`、`TotpField`。
- 减少卡片噪音，改为分组字段行。
- 增加详情页主操作。
- 构建和手动验证后提交 git。

提交建议：

```text
Refine item detail layout
```

### Phase 5: 编辑表单重构

- 按项目类型重组新建/编辑 dialog。
- 改善字段顺序和校验反馈。
- 保存失败保持 dialog 打开。
- 构建和手动验证后提交 git。

提交建议：

```text
Improve item editor forms
```

## 验证要求

每个 Phase 完成后至少执行：

```powershell
dotnet build .\BitwardenForReactor.csproj -p:Platform=x64
```

如果本机 `mur check` 可用，再执行：

```powershell
mur check -- -p:Platform=x64
```

手动验证重点：

- 启动、解锁、同步、锁定仍正常。
- 搜索和筛选行为不回退。
- 设置保存后重启仍生效。
- 复制密码、复制隐藏字段、复制 TOTP 正常。
- 新建、编辑、删除、恢复流程不受 UI 重构影响。

## 当前不做

- 不讨论 DataGrid 或其他表格控件选型。
- 不做动画系统。
- 不做自定义 NavigationView 模板。
- 不重写服务层和业务状态模型。
- 不引入重型 UI 框架。
