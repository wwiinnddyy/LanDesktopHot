# LanDesktopHot - 阑山桌面热榜插件

## 1. 需求概述

开发一个适用于阑山桌面 (LanMountainDesktop) 的热榜插件，第一期实现 **知乎热榜** 组件。组件尺寸为 4×4 单元格，风格参考阑山桌面内置的凤凰网资讯组件，使用 `FluentIcons.Avalonia` NuGet 包提供的图标。

同时包含 GitHub Actions 自动构建工作流，构建产物为 `.laapp格式插件包。

## 2. 架构与技术方案

### 2.1 整体架构

```
LanDesktopHot/
├── LanDesktopHot.csproj          # 项目文件
├── Plugin.cs                      # 插件入口，组件注册
├── plugin.json                    # 插件清单
├── NuGet.config                   # NuGet 源配置
├── Directory.Packages.props       # 中心包版本管理
├── GlobalUsings.cs                # 全局引用
├── Localization/
│   ├── zh-CN.json                 # 中文本地化
│   └── en-US.json                 # 英文本地化
├── Widgets/
│   ├── ZhihuHotListWidget.axaml   # 知乎热榜组件 AXAML 布局
│   └── ZhihuHotListWidget.axaml.cs # 知乎热榜组件代码
├── Models/
│   └── ZhihuHotItem.cs            # 知乎热榜数据模型
├── Services/
│   └── ZhihuHotListService.cs     # 知乎热榜数据获取服务
├── Messages/
│   └── ZhihuDataRefreshMessage.cs # 数据刷新消息
├── Assets/
│   └── icon.png                   # 插件图标
└── .github/
    └── workflows/
        └── build.yml              # GitHub Actions 构建工作流
```

### 2.2 技术栈

- **框架**: .NET 10 (net10.0)  - 与 阑山桌面 SDK v5 对齐
- **UI**: Avalonia 12.0.x + FluentIcons.Avalonia 2.1.325
- **SDK**: LanMountainDesktop.PluginSdk 5.0.0
- **HTTP**: System.Net.Http.HttpClient
- **JSON**: System.Text.Json

### 2.3 知乎热榜数据获取方案

知乎热榜的获取方式经调研确认：

1. **API 地址**: `https://www.zhihu.com/api/v3/feed/topstory/hot-lists/total`
   - 此接口需要特殊请求头 (x-zse-96, x-zse-93等加密参数)，直接调用复杂且易失效
   
2. **HTML 页面解析方案**（采用此方案）:
   - 请求 `https://www.zhihu.com/hot` 页面
   - 使用合适的 User-Agent（移动端 UA 更稳定）
   - 从 HTML 中提取 `<script id="js-initialData" type="text/json">` 内的 JSON 数据
   - 解析路径: `initialState.topstory.hotList`
   
3. **每个热榜条目结构**:
   ```json
   {
     "type": "hot-list",
     "cardId": "QjEwOTI4NTU5MjA=",
     "target": {
       "id": 1092855920,
       "titleArea": { "text": "问题标题" },
       "excerptArea": { "text": "问题描述" },
       "imageArea": { "url": "图片URL" },
       "metricsArea": { "text": "热度值" },
       "link": { "url": "问题链接" }
     }
   }
   ```

## 3. 详细实现

### 3.1 插件入口 (Plugin.cs)

- 类 `LanDesktopHotPlugin` 继承 `PluginBase`，标记 `[PluginEntrance]`
- `Initialize` 方法中注册：
  - `ZhihuHotListService`（Singleton）- 数据服务
  - `ZhihuHotListWidget` 组件（4×4 单元格）
- 使用 `PluginLocalizer` 支持中英文

### 3.2 组件注册选项

```csharp
new PluginDesktopComponentOptions
{
    ComponentId = "LanDesktopHot.ZhihuHotList",
    DisplayName = "知乎热榜",
    DisplayNameLocalizationKey = "widget.zhihu.display_name",
    IconKey = "Globe",       // FluentIcons 的 Globe 图标
    Category = "热榜",
    MinWidthCells = 4,
    MinHeightCells = 4,
    AllowDesktopPlacement = true,
    AllowStatusBarPlacement = false,
    ResizeMode = PluginDesktopComponentResizeMode.Proportional,
    CornerRadiusPreset = PluginCornerRadiusPreset.Default
}
```

### 3.3 数据服务 (ZhihuHotListService.cs)

```
ZhihuHotListService
├── Fields:
│   ├── _httpClient: HttpClient       # HTTP客户端
│   └── _cache: List<ZhihuHotItem>?    # 数据缓存
├── Public Methods:
│   ├── FetchAsync(): Task<List<ZhihuHotItem>>
│   └── GetCachedData(): List<ZhihuHotItem>?
└── Private Methods:
    └── ParseFromHtml(html): List<ZhihuHotItem>
        - 从 HTML 提取 js-initialData JSON
        - 解析 hotList 数组
        - 映射到 ZhihuHotItem 列表
```

#### 数据流:
1. 组件加载 → 调用 `FetchAsync()`
2. `FetchAsync` 请求 `https://www.zhihu.com/hot`（User-Agent: 移动端 Safari）
3. 从 HTML 提取 `js-initialData` JSON
4. 解析 JSON 提取 `initialState.topstory.hotList`
5. 映射到 `List<ZhihuHotItem>`
6. 缓存并返回结果
7. 失败时抛出异常，组件显示错误状态

### 3.4 数据模型 (ZhihuHotItem.cs)

```csharp
public sealed record ZhihuHotItem(
    int Rank,                    // 排名
    string Title,                // 标题
    string? Description,         // 描述/摘要
    string? HotMetrics,          // 热度值文本（如 "1000 万热度"）
    string? ImageUrl,            // 图片 URL
    string Url,                  // 问题链接
    long QuestionId              // 问题 ID
);
```

### 3.5 组件 UI 设计 (4×4 单元格)

#### AXAML 布局结构
```
UserControl (ZhihuHotListWidget)
└── Border (RootBorder)
    └── Grid (RowDefinitions="Auto,*")
        ├── HeaderGrid (Row 0, ColumnDefinitions="Auto,*,Auto")
        │   ├── Border (HeaderIconBadge)
        │   │   └── fi:SymbolIcon (Symbol.Globe, Filled)
        │   ├── StackPanel (HeaderStack, Column 1)
        │   │   ├── TextBlock (HeaderText) "知乎热榜"
        │   │   └── TextBlock (UpdateTimeText) "更新于 HH:mm"
        │   └── Button (RefreshButton, Column 2)
        │       └── fi:SymbolIcon (Symbol.ArrowSync, Regular)
        ├── ScrollViewer (HotListScrollViewer, Row 1)
        │   └── StackPanel (HotListStackPanel, Spacing=6)
        │       └── [动态生成的热榜条目卡片]
        ├── Border (LoadingHost) - 加载状态
        ├── Border (ErrorHost) - 错误状态
        └── Border (EmptyHost) - 空数据状态
```

#### 每个热榜条目卡片 (代码生成)
```
Border (hot-item-card)
└── Grid (ColumnDefinitions="Auto,*,Auto")
    ├── Border (RankBadge, Column 0)
    │   └── TextBlock (排名数字, Top3 高亮)
    ├── StackPanel (Column 1)
    │   ├── TextBlock (Title, 最多2行, 加粗)
    │   └── TextBlock (HotMetrics, 灰色小字)
    └── TextBlock (热度值, Column 2, 可选)
```

#### 与 SamplePluginStatusClockWidget 的相似之处:
- 同为 4×4 单元格尺寸
- 使用 `PluginDesktopComponentContext` 获取服务和上下文
- 使用 `PluginAppearanceSnapshot` 响应主题切换
- 使用 `GetLayoutBasis()` 进行动态缩放
- 使用 `AttachedToVisualTree` / `DetachedFromVisualTree` 管理生命周期
- 使用 `IPluginMessageBus` 接收刷新消息

#### 与凤凰网资讯组件的相似之处:
- 标题 + 时间头部区域
- 可滚动的内容列表
- 刷新按钮
- 加载/错误/空数据状态切换
- 圆角面板风格

### 3.6 主题支持

- 通过 `_context.Appearance.Snapshot.ThemeVariant` 检测明暗主题
- 使用 `_context.CornerRadiusTokens.Component` 获取组件圆角
- 明暗主题分别定义颜色调色板

### 3.7 本地化

- 中文: `Localization/zh-CN.json`
- 英文: `Localization/en-US.json`
- 使用 `PluginLocalizer` 加载

### 3.8 插件清单 (plugin.json)

```json
{
  "id": "LanDesktopHot",
  "name": "LanDesktopHot - 热榜插件",
  "description": "为阑山桌面提供知乎等平台的热榜资讯组件。",
  "author": "LanDesktopHot",
  "version": "0.1.0",
  "apiVersion": "5.0.0",
  "entranceAssembly": "LanDesktopHot.dll",
  "runtime": { "mode": "in-proc" }
}
```

## 4. NuGet 包引用

`LanDesktopHot.csproj` 的包引用（与 VoiceHubLanDesktop 对齐）：

| 包名 | 版本 | 备注 |
|------|------|------|
| `Avalonia` | 12.0.1 | ExcludeAssets="runtime" |
| `Avalonia.Desktop` | 12.0.1 | ExcludeAssets="runtime" |
| `Avalonia.Themes.Fluent` | 12.0.1 | ExcludeAssets="runtime" |
| `FluentIcons.Avalonia` | 2.1.325 | ExcludeAssets="runtime" |
| `FluentIcons.Avalonia.Fluent` | 2.0.325 | ExcludeAssets="runtime" |
| `LanMountainDesktop.PluginSdk` | 5.0.0 | ExcludeAssets="runtime", PrivateAssets="all" |
| `SkiaSharp.NativeAssets.Win32` | 3.119.x | ExcludeAssets="all" |
| `HarfBuzzSharp.NativeAssets.Win32` | 8.3.x | ExcludeAssets="all" |

## 5. 组件状态机

```
                    ┌──────────┐
                    │  Loading │ ← 初始状态、刷新时
                    └────┬─────┘
                         │
              ┌──────────┼──────────┐
              ▼          ▼          ▼
         ┌────────┐ ┌────────┐ ┌──────────┐
         │ Normal │ │ Error  │ │   Empty  │
         └────────┘ └────────┘ └──────────┘
              │
              ├── 点击条目 → 用系统浏览器打开链接
              ├── 点击刷新 → 重新 FetchAsync
              └── 定时刷新 → 每 5 分钟自动刷新
```

## 6. GitHub Actions 工作流

`.github/workflows/build.yml`:

- **触发器**: push 到 main 分支、PR、手动触发 (workflow_dispatch)
- **环境**: ubuntu-latest, .NET 10 SDK
- **步骤**:
  1. Checkout 代码
  2. Setup .NET 10
  3. 恢复 NuGet 包
  4. 构建项目 (`dotnet build`)
  5. 运行测试（如果有）
  6. 上传构建产物（.laapp 文件）为 Artifact

## 7. 边界条件与异常处理

| 场景 | 处理方式 |
|------|----------|
| 网络不可用 | 显示错误状态，提示"网络连接失败" |
| 知乎页面改版/解析失败 | 显示错误状态，提示"数据解析失败" |
| 返回空数据 | 显示空数据状态 |
| HTTP 超时 | 设置 HttpClient Timeout=10s，超时显示网络错误 |
| 组件销毁 | 取消 CancellationToken，清理定时器 |
| 主题切换 | 通过 `Appearance.Changed` 事件重新应用主题颜色 |
| 组件大小变化 | 通过 `SizeChanged` 事件重新计算缩放 |

## 8. 文件修改清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `LanDesktopHot.slnx` | 创建 | 解决方案文件 |
| `LanDesktopHot.csproj` | 创建 | 项目文件 |
| `plugin.json` | 创建 | 插件清单 |
| `NuGet.config` | 创建 | NuGet 包源配置 |
| `Plugin.cs` | 创建 | 插件入口 |
| `GlobalUsings.cs` | 创建 | 全局引用 |
| `Widgets/ZhihuHotListWidget.axaml` | 创建 | 组件 AXAML 布局 |
| `Widgets/ZhihuHotListWidget.axaml.cs` | 创建 | 组件代码 |
| `Models/ZhihuHotItem.cs` | 创建 | 数据模型 |
| `Services/ZhihuHotListService.cs` | 创建 | 数据服务 |
| `Messages/ZhihuDataRefreshMessage.cs` | 创建 | 消息定义 |
| `Localization/zh-CN.json` | 创建 | 中文本地化 |
| `Localization/en-US.json` | 创建 | 英文本地化 |
| `.github/workflows/build.yml` | 创建 | CI/CD 工作流 |
| `Assets/icon.png` | 创建 | 插件图标（占位） |
| `README.md` | 创建 | 项目说明 |

## 9. 预期成果

1. 插件可在阑山桌面中安装，显示"知乎热榜"组件
2. 组件以 4×4 格子显示，含头部（图标+标题+刷新按钮）和可滚动热榜列表
3. 每次进入组件页面、点击刷新、定时自动刷新知乎热榜数据
4. 支持明暗主题切换
5. 点击榜单条目通过系统浏览器打开知乎问题页
6. GitHub Actions 自动构建并产出版本