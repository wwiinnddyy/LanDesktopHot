namespace LanDesktopHot;

[PluginEntrance]
public sealed class LanDesktopHotPlugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ZhihuHotListService>();

        services.AddPluginDesktopComponent<Widgets.ZhihuHotListWidget>(new PluginDesktopComponentOptions
        {
            ComponentId = "zhihu-hotlist",
            DisplayName = "知乎热榜",
            DisplayNameLocalizationKey = "widget.zhihu.display_name",
            IconKey = "Globe",
            Category = "资讯",
            MinWidthCells = 4,
            MinHeightCells = 4,
            AllowDesktopPlacement = true,
            AllowStatusBarPlacement = false,
            ResizeMode = PluginDesktopComponentResizeMode.Free,
            CornerRadiusPreset = PluginCornerRadiusPreset.Component
        });
    }
}
