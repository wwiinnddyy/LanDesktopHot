# LanDesktopHot

为阑山桌面（LanMountainDesktop）提供知乎热榜桌面组件。

## 功能

- 4×4 默认尺寸，可自由调整大小。
- 自动读取知乎热榜，网络失败时保留最近一次有效数据。
- 每 5 分钟自动刷新，也可以手动刷新。
- 跟随宿主明暗主题与 Plugin SDK 5 圆角令牌。
- 点击榜单条目后用系统浏览器打开详情。
- 中英文资源随插件包发布。

## 兼容版本

- .NET 10
- LanMountainDesktop.PluginSdk 5.0.0
- Avalonia 12.1.0
- FluentAvaloniaUI 3.0.1
- FluentIcons.Avalonia 2.1.331
- 最低宿主版本 0.8.6

本仓库只使用生产插件清单 `plugin.json`。旧版 `airapp.json` 和
`LanMountainDesktop.AirAppSdk` 已淘汰，不会进入构建或发布包。

## 本地构建

将 `LanMountainDesktop` 克隆到本仓库同级目录后运行：

```powershell
.\scripts\build-local.ps1
```

恢复过程固定使用仓内 `.nuget/packages`，并通过 `--force --no-cache` 避免同版本
SDK 包受到机器级全局缓存污染。

脚本会打包当前宿主源码中的 Plugin SDK 及其 contracts，执行 restore/build、
校验包内清单，并生成：

- `LanDesktopHot.0.2.0.laapp`
- `artifacts/market-manifest.json`（市场 release manifest schema v2）
- `artifacts/sha256.txt`
- `artifacts/md5.txt`

如果 `packages/` 已经包含可用的 SDK 5 本地包，可以跳过重新打包 SDK：

```powershell
.\scripts\build-local.ps1 -SkipLocalPackageFeed
```

也可以只运行一致性校验：

```powershell
.\scripts\Test-PluginConsistency.ps1 `
  -RepositoryRoot (Resolve-Path ".").Path `
  -PackagePath .\LanDesktopHot.0.2.0.laapp
```

## 发布

- `LanDesktopHot Plugin CI` 在 push / PR 时构建、验证并上传插件包和市场 v2 清单。
- `LanDesktopHot Plugin Release` 为手动工作流；输入必须与 `plugin.json` 完全一致的版本号。
- Release 默认创建 draft prerelease，安装验证通过后再由维护者提升为正式版本。

## 项目结构

- `Plugin.cs`：Plugin SDK 5 入口和桌面组件注册。
- `Widgets/ZhihuHotListWidget.*`：组件界面与交互。
- `Services/ZhihuHotListService.cs`：知乎数据请求、解析与缓存。
- `Localization/`：中英文资源。
- `scripts/`：SDK feed、构建打包、市场 v2 和一致性校验脚本。

## 许可证

[GPL-3.0](LICENSE)
