# BitwardenForReactor

使用 [Microsoft.UI.Reactor](https://microsoft.github.io/microsoft-ui-reactor/) 和 WinUI 3 开发的中文 Bitwarden 桌面客户端。

项目通过 Bitwarden CLI（`bw`）访问密码库，界面采用 Reactor 的声明式组件和 MVU 状态管理，不使用 XAML 或传统 ViewModel 架构。

## 当前进度

核心功能已经可以使用：

- 检查登录状态、解锁、锁定和同步密码库
- 浏览、搜索和筛选登录、卡片、身份、安全笔记、收藏、归档及回收站项目
- 查看项目详情，复制用户名、密码、TOTP 等字段
- 新建、编辑、克隆、收藏、归档和删除项目
- 从回收站恢复或永久删除项目
- 生成密码、密码短语和用户名，并复制生成结果
- 配置 Bitwarden CLI 路径、认证环境和剪贴板清除时间
- 可持久化的跟随系统、浅色和深色主题选择
- 树状侧边导航、列表快捷操作、详情卡片和 Toolkit 设置控件

目前主要工作是继续优化各页面的布局、控件一致性和交互细节。附件、导入导出等入口尚未全部完成。

## 运行要求

- Windows 10 1809 或更高版本
- .NET SDK 10
- 已安装并可调用 [Bitwarden CLI](https://bitwarden.com/help/cli/)

首次使用前在终端完成登录：

```powershell
bw login
```

然后构建并运行：

```powershell
dotnet restore
dotnet run -p:Platform=x64
```

项目当前使用 `Microsoft.UI.Reactor 0.1.0-preview.11`。主要设计和开发记录位于 [`docs/`](docs/)。

## 项目结构

- `Application/`：异步命令和副作用编排
- `Components/`、`Pages/`、`Shell/`：Reactor UI 组件与应用外壳
- `State/`：应用状态、Action 和纯 Reducer
- `Services/`：Bitwarden CLI、设置、剪贴板和图标服务
- `Models/`：密码库数据模型和编辑草稿
- `Controls/Toolkit/`：Windows Community Toolkit 的 Reactor 适配器
