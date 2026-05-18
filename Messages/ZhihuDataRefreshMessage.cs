namespace LanDesktopHot.Messages;

/// <summary>
/// Message published when the Zhihu hot list data should be refreshed.
/// </summary>
public sealed class ZhihuDataRefreshMessage
{
    public static readonly ZhihuDataRefreshMessage Instance = new();

    private ZhihuDataRefreshMessage()
    {
    }
}