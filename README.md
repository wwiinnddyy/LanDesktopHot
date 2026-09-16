# LanDesktopHot

为阑山桌面（LanMountainDesktop）提供知乎热榜桌面组件。

## 功能

- 4×4 默认尺寸，可自由调整大小。
- 自动读取知乎热榜，网络失败时保留最近一次有效数据。
- 每 5 分钟自动刷新，也可以手动刷新。
- 跟随宿主明暗主题与圆角令牌。
- 点击榜单条目后用系统浏览器打开详情。
- 中英文资源随包发布。

## 兼容版本

- .NET 10
- LanMountainDesktop.AirAppSdk 1.0.0（发布在 GitHub Packages）
- 最低宿主版本 0.9.1

本仓库使用唯一的轻应用清单 `airapp.json`。旧版 `plugin.json` 和
`LanMountainDesktop.PluginSdk` 已淘汰，不会进入构建或发布包。

## 本地构建

需要安装 .NET SDK 10，并添加一次带 `read:packages` 权限 PAT 的 GitHub Packages 源：

```bash
dotnet nuget add source https://nuget.pkg.github.com/wwiinnddyy/index.json \
  --name lanmountain --username <你的GitHub用户名> --password <PAT> --store-password-in-clear-text

dotnet build LanDesktopHot.csproj -c Release
```

AirApp SDK 的 MSBuild 目标会在构建后自动生成 `LanDesktopHot.<版本号>.laapp`。

## 发布

- `.github/workflows/ci.yml`：push / PR / 手动触发时构建并上传 `.laapp`。
- `.github/workflows/release.yml`：推 `v*` tag 或手动触发时构建 `.laapp` 并创建 GitHub Release。

## 项目结构

- `Plugin.cs`：AirApp 入口和桌面组件注册。
- `Widgets/ZhihuHotListWidget.*`：组件界面与交互。
- `Services/ZhihuHotListService.cs`：知乎数据请求、解析与缓存。
- `Localization/`：中英文资源。

## 许可证

[GPL-3.0](LICENSE)
