# 用 Microsoft.UI.Reactor 开发 WinUI 3 应用的实践指南

这篇文章面向已经熟悉 C# / WinUI 3，但还没有系统使用过 Microsoft.UI.Reactor 的开发者。配套的 `skills/` 文件夹可以作为离线参考资料：遇到具体问题时，优先查 `reactor-getting-started`、`reactor-dsl`、`reactor-design`、`reactor-navigation`、`reactor-async` 和 `reactor-build-and-check`。

## 1. Reactor 是什么

Microsoft.UI.Reactor 可以理解为 WinUI 3 上的声明式 UI 层。传统 WinUI 通常写 XAML + code-behind 或 MVVM；Reactor 则让你用 C# 直接描述 UI 树：

```csharp
FlexColumn(
    TitleBar("DataViewer"),
    Button("打开文件", OnOpen)
)
.Backdrop(BackdropKind.Mica);
```

它的核心概念有几个：

- `Component`：一个可渲染的 UI 单元，类似 React component。
- `Element`：声明式 UI 描述，不是实际 WinUI 控件实例。
- `Render()`：根据当前状态返回 UI 树。
- Hooks：例如 `UseState`、`UseReducer`、`UseNavigation`。
- Modifier：例如 `.Padding(16)`、`.Flex(...)`、`.Background(Theme.CardBackground)`。
- `Theme.*`：主题 token，避免在深浅色模式下硬编码颜色。

开发时建议把 `reactor-dsl/references/reactor.api.txt` 当作 API 索引，不确定某个控件或 modifier 是否存在时直接查它。构建和诊断参考 `reactor-build-and-check`。

## 2. 项目结构与状态设计

Reactor 项目不需要 XAML 文件也能构建完整 WinUI 应用。一个清晰的结构通常是：

```text
App.cs                 # 应用 shell 和页面组合
Controls/             # 可复用 Reactor 控件或第三方控件包装
Data/                 # 数据读取抽象
Services/             # DuckDB、文件、业务服务
State/                # AppState、AppAction、Reducer
CodeMirror/           # WebView2 编辑器本地 assets
skills/               # Reactor 本地参考文档
```

### 接入社区控件

社区 WinUI 控件可以通过 Reactor reconciler 注册，而不是塞进 XAML：

```csharp
reconciler.RegisterType<ResultTableViewElement, TableView>(
    mount: (_, element, _) =>
    {
        var table = new TableView();
        ApplyResult(table, element.Result);
        return table;
    },
    update: (_, oldElement, newElement, table, _) =>
    {
        if (!ReferenceEquals(oldElement.Result, newElement.Result))
        {
            ApplyResult(table, newElement.Result);
        }

        return table;
    },
    unmount: (_, table) =>
    {
        table.ItemsSource = null;
        table.Columns.Clear();
    });
```

这比 `XamlHostElement` 更适合长期维护：生命周期清楚，属性可以声明式建模，事件也能映射回 Reactor 状态。

### 用 record 建模

Reactor 很适合搭配不可变数据。数据结构优先用 `record`：

```csharp
public sealed record AppState(
    LoadedDataSet? DataSet,
    string SqlText,
    QueryResult? Result,
    bool IsBusy,
    string? ErrorMessage);

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    int DisplayedRowCount);
```

状态变化通过 action 表达：

```csharp
public abstract record AppAction;
public sealed record FileLoaded(LoadedDataSet DataSet, string SqlText, QueryResult Result) : AppAction;
public sealed record QueryFailed(string Message) : AppAction;
```

Reducer 保持纯函数：

```csharp
public static AppState Reduce(AppState state, AppAction action) =>
    action switch
    {
        FileLoaded loaded => state with
        {
            DataSet = loaded.DataSet,
            SqlText = loaded.SqlText,
            Result = loaded.Result,
            IsBusy = false,
            ErrorMessage = null,
        },
        QueryFailed failed => state with
        {
            IsBusy = false,
            ErrorMessage = failed.Message,
        },
        _ => state,
    };
```

副作用不要放进 reducer。文件选择、DuckDB 查询、WebView2 调用等都放在按钮事件或服务层里，成功/失败后再 dispatch action。这就是 MVU 风格：`State + Action + Reducer + Render`。

## 3. 进阶实践：布局、导航与复杂控件

### 布局

Reactor 里常用 `FlexRow` / `FlexColumn` 做主布局：

```csharp
FlexRow(
    Sidebar().Flex(shrink: 0, basis: 280),
    Workspace().Flex(grow: 1, basis: 0)
) with { ColumnGap = 16 };
```

页面级区域用 `Border` 或全宽布局承载，局部重复内容再用 `Card`。不要为了视觉效果层层套卡片。主题颜色优先使用：

```csharp
.Background(Theme.CardBackground)
.WithBorder(Theme.CardStroke, 1)
.Foreground(Theme.SecondaryText)
```

更多视觉规范查 `reactor-design`。

### 导航

标准导航使用 `UseNavigation` + `NavigationHost` + `NavigationView`：

```csharp
var nav = UseNavigation(AppRoute.Query);

return NavigationView(
    [
        NavItem("查询", icon: "Library", tag: "query"),
    ],
    NavigationHost(nav, route => route switch
    {
        AppRoute.Query => RenderQueryPage(),
        AppRoute.Settings => RenderSettingsPage(),
        _ => TextBlock("Not found"),
    })
);
```

如果使用 settings 入口，官方示例通常启用 `IsSettingsVisible = true`，再在 `SelectionChanged` 判断 `args.IsSettingsSelected`。复杂视觉样式不要急着重写 `NavigationViewItem` 模板；先确认需求是“标准导航”还是“完全自绘导航 shell”。后者更适合抽成独立 Reactor 组件。

导航细节查 `reactor-navigation`。

### WebView2 编辑器

接入 CodeMirror / Monaco 这类编辑器时，建议：

- 用 WebView2 承载前端编辑器。
- 用 `RegisterType` 封装成 Reactor element。
- 用 `window.chrome.webview.postMessage` 把编辑器事件发回 C#。
- 用 `ExecuteScriptAsync` 或初始化配置把 state 推给编辑器。
- 发布版把 JS assets vendored 到本地，并用 `SetVirtualHostNameToFolderMapping` 加载。
- AOT 场景下 JSON 序列化使用 `JsonSerializerContext`。

这类控件不要每个按键都 dispatch 全局状态，否则 WebView 容易闪烁。可以让编辑器维护即时文本，执行查询或保存时再同步到 MVU state。

## 推荐阅读顺序

1. `skills/reactor-getting-started/SKILL.md`
2. `skills/reactor-dsl/references/reactor.api.txt`
3. `skills/reactor-design/SKILL.md`
4. `skills/reactor-navigation/SKILL.md`
5. `skills/reactor-async/SKILL.md`
6. `skills/reactor-build-and-check/SKILL.md`

实际开发中，先让状态结构和组件边界稳定，再打磨视觉细节。Reactor 的优势不是少写几行 UI，而是让 WinUI 应用也能保持“状态驱动、结构清晰、可逐步演化”的工程形态。
