namespace LanDesktopHot;

[AirAppEntrance]
public sealed class LanDesktopHotPlugin : AirAppBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ZhihuHotListService>();

        services.AddAirAppComponent<Widgets.ZhihuHotListWidget>(
            "zhihu-hotlist",
            "知乎热榜",
            options =>
            {
                options.Description = "实时展示知乎首页热榜";
                options.DefaultWidth = 4;
                options.DefaultHeight = 4;
                options.ResizeMode = AirAppComponentResizeMode.Both;
                options.Category = "资讯";
                options.IconKey = "Globe";
            });
    }

    public override Task OnStartedAsync(IAirAppRuntimeContext context)
    {
        context.Logger.Info("LanDesktopHot AirApp started successfully!");
        return Task.CompletedTask;
    }

    public override Task OnStoppingAsync()
    {
        return Task.CompletedTask;
    }
}