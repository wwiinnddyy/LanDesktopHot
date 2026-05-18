# LanDesktopHot - 阑山桌面热榜插件

为 [阑山桌面 (LanMountainDesktop)](https://github.com/wwiinnddyy/LanMountainDesktop) 提供知乎等平台的热榜资讯组件。

## 功能

- **知乎热榜**：实时展示知乎首页热榜，支持点击跳转问题页
- 4×4 单元格组件，响应式缩放
- 支持明暗主题切换
- 定时自动刷新（每 5 分钟）
- 支持中英文

## 安装

1. 从 [Releases](https://github.com/wwiinnddyy/LanDesktopHot/releases) 下载最新的 `.laapp` 文件
2. 在阑山桌面中双击安装，或通过插件市场安装程序导入
3. 在组件库中找到"知乎热榜"组件，添加到桌面

## 构建

```bash
dotnet restore
dotnet build -c Release
```

构建产物位于 `bin/Release/net10.0/content/` 目录下，包含 `.laapp` 插件包。

## 项目结构

```
LanDesktopHot/
├── Plugin.cs                      # 插件入口
├── plugin.json                    # 插件清单
├── Widgets/
│   └── ZhihuHotListWidget.axaml   # 知乎热榜组件
│   └── ZhihuHotListWidget.axaml.cs
├── Models/
│   └── ZhihuHotItem.cs            # 数据模型
├── Services/
│   └── ZhihuHotListService.cs     # 数据获取服务
├── Messages/
│   └── ZhihuDataRefreshMessage.cs # 刷新消息
├── Localization/                  # 本地化文件
│   ├── zh-CN.json
│   └── en-US.json
└── .github/workflows/
    └── build.yml                  # CI/CD 工作流
```

## 技术栈

- .NET 10 / Avalonia 12
- FluentIcons.Avalonia 2.1.325
- LanMountainDesktop.PluginSdk 5.0.0

## 免责声明

本插件通过解析知乎公开页面获取数据，仅供学习研究使用。请遵守知乎相关服务条款。

## 许可证

[GPL-3.0](LICENSE)