# LanDesktopHot 热榜插件 - 实现任务计划

- [x] Task 1: 创建解决方案和项目基础结构
    - 1.1: 创建 `.comate/specs/lan-desktop-hot/` 目录
    - 1.2: 创建项目根目录结构（if not exists）
    - 1.3: 创建 `LanDesktopHot.slnx` 解决方案文件
    - 1.4: 创建 `LanDesktopHot.csproj` 项目文件（net10.0, EnableDynamicLoading, FluentIcons.Avalonia 等包引用）
    - 1.5: 创建 `NuGet.config`（nuget.org + local-packages 源）
    - 1.6: 创建 `GlobalUsings.cs` 全局引用
    - 1.7: 创建 `plugin.json` 插件清单

- [x] Task 2: 创建插件入口和组件注册
    - 2.1: 创建 `Plugin.cs` - 插件入口类 `LanDesktopHotPlugin`，标记 `[PluginEntrance]`
    - 2.2: 实现 `Initialize` 方法 - 注册 `ZhihuHotListService` 和 `ZhihuHotListWidget`
    - 2.3: 实现组件选项配置（4×4 单元格，Globe 图标，Proportional 缩放）
    - 2.4: 实现 `PluginLocalizer` 创建方法

- [x] Task 3: 创建数据模型和服务
    - 3.1: 创建 `Messages/ZhihuDataRefreshMessage.cs` 消息定义
    - 3.2: 创建 `Models/ZhihuHotItem.cs` 数据模型
    - 3.3: 创建 `Services/ZhihuHotListService.cs` - HTML 抓取、JSON 解析、数据缓存
    - 3.4: 实现异常处理和超时逻辑

- [x] Task 4: 创建知乎热榜组件 UI
    - 4.1: 创建 `Widgets/ZhihuHotListWidget.axaml` - AXAML 布局（Header + ScrollViewer + 状态层）
    - 4.2: 创建 `Widgets/ZhihuHotListWidget.axaml.cs` - 组件逻辑（生命周期、数据加载、主题切换、缩放）
    - 4.3: 实现热榜条目卡片动态生成（排名徽标、标题、热度值）
    - 4.4: 实现点击条目打开浏览器功能
    - 4.5: 实现刷新按钮功能

- [x] Task 5: 创建本地化文件
    - 5.1: 创建 `Localization/zh-CN.json` 中文翻译
    - 5.2: 创建 `Localization/en-US.json` 英文翻译

- [x] Task 6: 创建 GitHub Actions 工作流
    - 6.1: 创建 `.github/workflows/build.yml` - 构建 + .laapp 打包
    - 6.2: 配置 .NET 10 SDK 安装、NuGet 恢复、dotnet build
    - 6.3: 配置构建产物上传为 Artifact

- [x] Task 7: 创建项目文档和资源
    - 7.1: 创建 `README.md` 项目说明
    - 7.2: 创建 `Assets/icon.png` 占位图标
    - 7.3: 创建 `airappmarket-entry.template.json` 市场条目模板

- [x] Task 8: 构建验证
    - 完成 ✓
    - 8.1: 执行 `dotnet restore` 验证包恢复 ✓
    - 8.2: 执行 `dotnet build` - Release 编译 0 错误 0 警告 ✓
    - 8.3: 生成 `.laapp` 插件包: `LanDesktopHot.0.1.0.laapp` ✓
    - 8.4: 生成 `summary.md` 总结文档
