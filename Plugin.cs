namespace LanDesktopHot;

[PluginEntrance]
public sealed class LanDesktopHotPlugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        var localizer = CreateLocalizer(context);

        // Register data service
        services.AddSingleton<ZhihuHotListService>();

        // Register the Zhihu hot list widget (4x4 cells)
        services.AddPluginDesktopComponent<Widgets.ZhihuHotListWidget>(
            CreateZhihuHotListComponentOptions(localizer));
    }

    private static PluginLocalizer CreateLocalizer(HostBuilderContext context)
    {
        var pluginDirectory = context.Properties.TryGetValue("LanMountainDesktop.PluginDirectory", out var directoryValue) &&
                              directoryValue is string resolvedPluginDirectory &&
                              !string.IsNullOrWhiteSpace(resolvedPluginDirectory)
            ? resolvedPluginDirectory
            : AppContext.BaseDirectory;

        var properties = context.Properties
            .Where(pair => pair.Key is string)
            .ToDictionary(pair => (string)pair.Key, pair => (object?)pair.Value, StringComparer.OrdinalIgnoreCase);

        return new PluginLocalizer(pluginDirectory, PluginLocalizer.ResolveLanguageCode(properties));
    }

    private static PluginDesktopComponentOptions CreateZhihuHotListComponentOptions(PluginLocalizer localizer)
    {
        return new PluginDesktopComponentOptions
        {
            ComponentId = "LanDesktopHot.ZhihuHotList",
            DisplayName = localizer.GetString("widget.zhihu.display_name", "知乎热榜"),
            DisplayNameLocalizationKey = "widget.zhihu.display_name",
            IconKey = "Globe",
            Category = localizer.GetString("widget.category", "热榜"),
            MinWidthCells = 4,
            MinHeightCells = 4,
            AllowDesktopPlacement = true,
            AllowStatusBarPlacement = false,
            ResizeMode = PluginDesktopComponentResizeMode.Proportional,
            CornerRadiusPreset = PluginCornerRadiusPreset.Default
        };
    }
}