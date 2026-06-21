# Bitwarden CLI 类库重构 TODO

最后更新：2026-06-21

## 目标

将当前项目中的 Bitwarden CLI 调用能力提取为独立、可测试、可打包的 .NET 类库，供 Reactor、传统 WinUI 3 以及其他 .NET 客户端复用。

类库必须：

- 不依赖 WinUI、Reactor、MVVM Toolkit 或具体应用状态模型。
- 支持通过 `BITWARDENCLI_APPDATA_DIR` 隔离多个账号。
- 不在命令行参数、日志或持久化配置中泄露主密码、Session、API Secret 和条目 JSON。
- 提供结构化结果和错误，不再以 `bool`、`null` 和错误字符串作为主要契约。
- 支持取消、超时、并发控制和 CLI 版本检查。
- 可以通过 NuGet 包被多个项目引用，不再复制源码文件。

## 仓库与项目结构

建议创建独立兄弟仓库：

```text
D:\dotnet\BitwardenCli.Core\
  src\
    BitwardenCli.Core\
      BitwardenCli.Core.csproj
  tests\
    BitwardenCli.Core.Tests\
      BitwardenCli.Core.Tests.csproj
    BitwardenCli.Core.IntegrationTests\
      BitwardenCli.Core.IntegrationTests.csproj
  samples\
    BitwardenCli.ConsoleSample\
  docs\
  Directory.Build.props
  README.md
  CHANGELOG.md
  LICENSE
  BitwardenCli.Core.slnx
```

首版只发布一个 `BitwardenCli.Core` 包。Windows Credential Locker、DPAPI 或具体 UI 适配器不进入 Core；如果后续确实有多个项目需要，再新增可选的 `BitwardenCli.Windows` 包。

目标框架建议使用 `net10.0`，避免无必要的 `-windows` 限制。所有公开 API 开启 nullable，并生成 XML 文档。

## 类库边界

### 移入类库

- CLI 可执行文件发现与版本检查。
- 进程启动、参数、环境变量、stdin/stdout/stderr。
- 账号 Profile 和独立 CLI 数据目录。
- Login、Logout、Status、Unlock、Lock、Sync。
- Vault、Folder、Attachment、Organization、Collection、Send 等命令封装。
- CLI JSON DTO、序列化上下文和错误模型。
- Session 的内存生命周期管理。
- 命令取消、超时、同账号串行化和诊断接口。

### 保留在客户端项目

- `AppState`、`AppAction`、Reducer 和 Reactor 组件。
- InfoBar、Dialog、加载状态和中文 UI 文案。
- 剪贴板服务、图标服务和应用自动锁定策略。
- 应用主题及其他 UI 设置。
- Windows Credential Locker/DPAPI 的具体实现。
- 当前应用账号切换器和账号管理页面。

### 需要拆分的现有设置

当前 `AppSettings` 同时包含 UI 设置和 CLI 设置，重构后拆为：

```text
ApplicationSettings
  ThemeMode
  ClipboardClearSeconds
  AutoLockMinutes
  ActiveAccountId

BitwardenCliOptions
  ExecutablePath
  DefaultTimeout
  AdditionalEnvironment

BitwardenAccountProfile
  Id
  DisplayName
  Email
  UserId
  ServerUrl
  CliDataDirectory
  AuthenticationKind
  LastUsedAt
```

账号 Profile 只保存非敏感元数据。主密码、Session 和 API Secret 不得进入 Profile JSON。

## 建议的公开 API

```csharp
public sealed class BitwardenCliClientFactory
{
    BitwardenCliClient Create(BitwardenAccountProfile profile);
}

public sealed class BitwardenCliClient
{
    BitwardenAccountProfile Profile { get; }
    BitwardenSessionState Session { get; }

    Task<CliResult<BitwardenStatus>> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<CliResult<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<CliResult<UnlockResult>> UnlockAsync(UnlockRequest request, CancellationToken cancellationToken = default);
    Task<CliResult> LockAsync(CancellationToken cancellationToken = default);
    Task<CliResult> LogoutAsync(CancellationToken cancellationToken = default);
}
```

按领域拆分命令，避免一个数千行 Service：

```text
client.Authentication
client.Vault
client.Folders
client.Attachments
client.Organizations
client.Sends
client.Generator
client.ImportExport
```

所有命令接收 `CancellationToken`，返回 `CliResult` 或 `CliResult<T>`。

## 结构化结果与错误

新增：

```text
CliResult<T>
  IsSuccess
  Value
  Error
  ExitCode
  StandardError
  Duration

CliError
  Code
  Message
  IsTransient

CliErrorCode
  ExecutableNotFound
  UnsupportedVersion
  InvalidArguments
  Unauthenticated
  VaultLocked
  InvalidMasterPassword
  TwoFactorRequired
  NetworkUnavailable
  Timeout
  Cancelled
  InvalidResponse
  Conflict
  PermissionDenied
  Unknown
```

错误分类可以读取 stderr，但字符串匹配必须集中在一个分类器中，并保留未知错误的原始信息。客户端负责把错误转换成中文 UI 文案。

## 多账号设计

每个账号必须使用独立 CLI 数据目录：

```text
%LocalAppData%\BitwardenForReactor\accounts\<account-id>\cli\data.json
```

类库不硬编码 `BitwardenForReactor` 路径。宿主应用创建 Profile 时传入绝对目录，类库负责验证、创建并在每次命令中设置：

```text
BITWARDENCLI_APPDATA_DIR=<profile.CliDataDirectory>
```

TODO：

- [ ] 定义 `BitwardenAccountProfile` 和稳定的 UUID 账号 ID。
- [ ] 校验 CLI 数据目录必须为绝对路径。
- [ ] 禁止两个 Profile 指向同一数据目录。
- [ ] 为每个 Profile 建立独立的 `BitwardenCliClient`。
- [ ] Session 使用 `ConcurrentDictionary<AccountId, SessionHandle>` 或 Client 实例字段保存在内存。
- [ ] 切换账号时不复制 Session，不修改其他账号的 CLI 目录。
- [ ] 显式锁定只影响目标账号；退出应用时 Session 随进程销毁。
- [ ] `logout` 只操作目标账号目录，并清除该账号内存 Session。
- [ ] 同一账号的写命令通过 `SemaphoreSlim` 串行化，避免并发修改 `data.json`。
- [ ] 不同账号允许并行执行命令。
- [ ] 自托管服务器配置按 Profile 隔离执行 `bw config server`。

## 敏感数据处理

- [ ] 解锁使用临时随机环境变量和 `bw unlock --passwordenv <name> --raw`，禁止把主密码作为参数。
- [ ] Session 通过子进程环境变量 `BW_SESSION` 传入，禁止使用 `--session <value>`。
- [ ] API Key 登录只在登录进程环境中注入 `BW_CLIENTID`、`BW_CLIENTSECRET`。
- [ ] `create`、`edit`、`move` 等 JSON 负载通过 stdin 传入，禁止放入命令行。
- [ ] Runner 日志默认只记录命令名、非敏感选项、耗时和退出码。
- [ ] 为参数、环境变量和 stdin 建立统一敏感标记与脱敏规则。
- [ ] Core 不持久化任何 Secret；宿主应用通过回调或 `ISecretProvider` 临时提供。
- [ ] 清理不再使用的字符数组/缓冲区；不承诺 CLR `string` 的绝对内存擦除。

## 进程执行层 TODO

- [ ] 定义 `CliCommand`：Arguments、Environment、StandardInput、Timeout、SensitiveFields。
- [ ] 定义 `IBitwardenCliRunner`，便于单元测试替换。
- [ ] 使用 `ProcessStartInfo.ArgumentList`，禁止自行拼接 shell 命令。
- [ ] 对所有非交互命令添加 `--nointeraction`。
- [ ] 并行读取 stdout/stderr，等待流完整结束。
- [ ] 支持 `CancellationToken` 和默认超时。
- [ ] 取消或超时时终止完整进程树。
- [ ] 检测 `bw` 不存在、无法启动和权限错误。
- [ ] 提供 `GetVersionAsync` 和最低支持版本检查。
- [ ] 暴露可选诊断事件，但事件内容必须脱敏。

## 命令覆盖 TODO

### 第一优先级：保持当前客户端功能

- [ ] `status`
- [ ] `login`：邮箱密码、API Key
- [ ] `logout`
- [ ] `unlock`、`lock`
- [ ] `sync`、`sync --force`、`sync --last`
- [ ] `list items`、trash、archived、folders
- [ ] `get item`、TOTP、username、password、URI、notes
- [ ] `create/edit/delete/restore/archive item`
- [ ] clone item
- [ ] 基础密码生成与口令生成

### 第二优先级：完整密码库编辑

- [ ] 创建、编辑、删除 Folder。
- [ ] 上传、下载、删除 Attachment。
- [ ] 列出 Organization、Collection、Org Collection。
- [ ] 修改 item collections。
- [ ] 将 item 移动到 Organization。
- [ ] 获取 template、fingerprint、exposed。

### 第三优先级：高级能力

- [ ] Import 格式发现与导入。
- [ ] CSV、JSON、encrypted JSON、ZIP Export。
- [ ] Send list/get/create/edit/delete/receive/remove-password。
- [ ] Device approval。
- [ ] Organization member confirm。

`bw serve` 不纳入首版类库。若以后需要常驻 REST 服务，应作为独立包和独立威胁模型处理。

## 认证流程 TODO

- [ ] 定义 `LoginRequest` 联合模型：Password、ApiKey、Sso。
- [ ] 邮箱密码登录支持 MFA method/code，但不做交互式 stdin 提示。
- [ ] API Key 登录从 `ISecretProvider` 获取 Secret。
- [ ] SSO 作为独立阶段实现浏览器流程，不阻塞核心重构。
- [ ] 登录后重新读取 `status`，以 CLI 返回的 userId/email/server 更新 Profile。
- [ ] 解锁后只保存 Session，不保存主密码。
- [ ] Session 失效时返回 `VaultLocked`，由客户端决定是否弹出解锁页。
- [ ] 明确账号切换、锁定和登出的不同语义。

## 模型策略

- [ ] 类库 DTO 完整对应 CLI JSON，不携带 UI 显示属性。
- [ ] 保留未知 JSON 字段的兼容策略，避免 CLI 升级导致数据丢失。
- [ ] 写入 item 时优先获取完整对象后修改，保留客户端暂未识别字段。
- [ ] 将 `VaultItemDraft` 留在 UI 项目，由适配器转换成类库写入模型。
- [ ] 使用 source-generated `System.Text.Json` context。
- [ ] 对日期、枚举和 nullable 字段补充真实 CLI fixture 测试。

## 测试计划

### 单元测试

- [ ] Runner 参数、环境变量和 stdin 组装。
- [ ] 敏感字段不会出现在参数或日志中。
- [ ] stderr 到 `CliErrorCode` 的映射。
- [ ] JSON 模型和 fixture 的往返序列化。
- [ ] Profile 路径验证和重复目录检测。
- [ ] 同账号串行、不同账号并行。
- [ ] 超时、取消、进程启动失败和无效 JSON。

### 集成测试

- [ ] 使用临时 `BITWARDENCLI_APPDATA_DIR`，不访问开发者默认账号。
- [ ] 两个临时 Profile 的 `status` 和 `data.json` 完全隔离。
- [ ] Profile A 配置 server 不影响 Profile B。
- [ ] Profile A 登录/登出不改变 Profile B 状态。
- [ ] Session A 不能用于 Profile B。
- [ ] 测试结束验证临时目录安全清理。
- [ ] 需要真实账号的测试使用显式环境开关，默认跳过。

## 打包与版本管理

- [ ] 设置 `PackageId`、Authors、Description、RepositoryUrl、License。
- [ ] 开启 `GeneratePackageOnBuild=false`，由发布流程显式 pack。
- [ ] 使用语义化版本，初始版本建议 `0.1.0`。
- [ ] `dotnet pack -c Release` 生成 `.nupkg` 和 symbols 包。
- [ ] 建立本地 feed，供多个客户端在开发阶段引用。
- [ ] 发布前决定使用 NuGet.org、GitHub Packages 或私有 feed。
- [ ] 每个版本维护 CHANGELOG 和迁移说明。
- [ ] 在 CI 中执行 restore、build、test、pack 和包内容检查。

## 当前 Reactor 应用迁移

- [ ] 先保持现有 UI 和 State 不变，新增薄适配器 `BitwardenApplicationService`。
- [ ] 适配器将 `CliResult` 转换为现有 AppAction 和中文 Notice。
- [ ] 用包引用替换当前 `Services/BitwardenCliService.cs`。
- [ ] 将现有 `BitwardenModels` 与类库 DTO 的重复类型逐步合并。
- [ ] 将 CLI 路径和账号配置从 `AppSettings` 中拆出。
- [ ] `AppState` 增加 `Accounts`、`ActiveAccountId` 和账号级状态。
- [ ] 切换账号时取消旧账号正在运行的加载任务并清空当前 vault state。
- [ ] 完成账号管理 UI 后再删除“请在终端执行 bw login”的旧流程。
- [ ] 删除旧 Service 前运行完整的浏览、编辑、删除、恢复、同步回归测试。

## 实施顺序与 Git 检查点

1. **建立类库仓库和 CI 骨架**  
   提交：`Initialize reusable Bitwarden CLI library`

2. **实现安全进程 Runner 与结构化错误**  
   提交：`Add secure Bitwarden CLI process runner`

3. **实现 Profile、目录隔离和 Session 生命周期**  
   提交：`Add isolated multi-account profiles`

4. **迁移认证、状态和同步命令**  
   提交：`Add account authentication and sync commands`

5. **迁移现有 Vault 读写命令**  
   提交：`Add vault item command client`

6. **补齐文件夹、归档和附件**  
   提交：`Add folder archive and attachment commands`

7. **发布本地 NuGet 包并接入 Reactor 应用**  
   类库提交：`Pack initial BitwardenCli.Core release`  
   应用提交：`Consume reusable Bitwarden CLI package`

8. **实现账号管理和切换 UI**  
   提交：`Add multi-account vault switching`

每完成一个大环节必须独立提交，不把类库架构、命令扩展和 UI 改造混在同一个提交中。

## 首版完成标准

- 两个账号能够同时保持已登录状态，并使用独立 CLI 数据目录。
- 两个账号的 Session、服务器配置、缓存和命令结果不会串号。
- 当前应用已有的全部 Vault 功能迁移后行为不回退。
- 主密码、Session、API Secret 和条目 JSON 不出现在进程参数及日志中。
- 类库不引用任何 WinUI/Reactor 类型。
- 单元测试通过，临时 Profile 集成测试通过。
- Reactor 应用仅通过 NuGet 包和薄适配器访问 CLI。
