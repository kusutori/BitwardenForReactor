# AGENTS.md

## 常用命令

```powershell
dotnet restore
dotnet build --no-restore -p:Platform=x64
dotnet run -p:Platform=x64
mur check -- -p:Platform=x64
mur check --final -- -p:Platform=x64
```

每完成一个独立的大环节后提交 Git。不要提交 `bin/`、`obj/` 或本机生成的敏感设置。

## Reactor 资料

- 官方文档：https://microsoft.github.io/microsoft-ui-reactor/
- NuGet：https://www.nuget.org/packages/Microsoft.UI.Reactor/
- 本机源码：`D:\dotnet\microsoft-ui-reactor`
- 官方变更记录：`D:\dotnet\microsoft-ui-reactor\CHANGELOG.md`
- 项目实践指南：`docs/reactor-development-guide.md`
- Reactor API 和技能资料：项目的 `.agents/skills/` 目录，或 NuGet 包中的 `agentkit/`

不确定 API 是否存在时，先搜索 `reactor.api.txt`，再查看 Reactor 源码实现。
