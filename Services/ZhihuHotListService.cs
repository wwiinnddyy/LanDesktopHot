using System.Diagnostics;
using System.Text.Json;
using LanDesktopHot.Models;

namespace LanDesktopHot.Services;

/// <summary>
/// Service that fetches Zhihu hot list data.
/// Uses the Zhihu hot-list-web API endpoint as primary data source,
/// with HTML page parsing as fallback.
/// </summary>
public sealed class ZhihuHotListService
{
    // Primary: Zhihu internal API endpoint (used by zhihu.com web/mobile)
    private const string ZhihuApiUrl = "https://www.zhihu.com/api/v3/feed/topstory/hot-list-web";

    // Fallback: HTML page scraping
    private const string ZhihuHotPageUrl = "https://www.zhihu.com/hot";
    private const string JsonDataMarker = "js-initialData";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;

    // Simple in-memory cache
    private List<ZhihuHotItem>? _cachedItems;

    public ZhihuHotListService()
    {
        _httpClient = new HttpClient
        {
            Timeout = RequestTimeout
        };
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.zhihu.com/hot");
    }

    /// <summary>
    /// Returns cached data if available without making a new request.
    /// </summary>
    public List<ZhihuHotItem>? GetCachedData() => _cachedItems;

    /// <summary>
    /// Fetches the latest Zhihu hot list from the API.
    /// Tries the JSON API first, falls back to HTML page parsing.
    /// </summary>
    public async Task<List<ZhihuHotItem>> FetchAsync(CancellationToken cancellationToken = default)
    {
        // Try API first
        try
        {
            var response = await _httpClient.GetAsync(ZhihuApiUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var items = ParseFromApi(json);
                if (items.Count > 0)
                {
                    _cachedItems = items;
                    return items;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"[ZhihuHotListService] API request failed: {ex.Message}, falling back to HTML parsing.");
        }

        // Fallback: parse from HTML page
        try
        {
            var html = await _httpClient.GetStringAsync(ZhihuHotPageUrl, cancellationToken);
            var items = ParseFromHtml(html);
            if (items.Count > 0)
            {
                _cachedItems = items;
                return items;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"[ZhihuHotListService] HTML fallback also failed: {ex.Message}");
        }

        // If we have cached data, return it as a last resort
        if (_cachedItems is { Count: > 0 })
        {
            return _cachedItems;
        }

        throw new InvalidOperationException("无法获取知乎热榜数据，请检查网络连接。");
    }

    /// <summary>
    /// Parses Zhihu hot list items from the JSON API response.
    /// API endpoint: /api/v3/feed/topstory/hot-list-web
    /// Response structure: { "data": [ { "target": { ... }, ... ] }
    /// Each item's "target" contains titleArea, excerptArea, metricsArea, link, etc.
    /// </summary>
    private static List<ZhihuHotItem> ParseFromApi(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var dataArray) ||
            dataArray.ValueKind != JsonValueKind.Array)
        {
            Debug.WriteLine("[ZhihuHotListService] 'data' array not found in API response.");
            return [];
        }

        var items = new List<ZhihuHotItem>(dataArray.GetArrayLength());
        var index = 0;

        foreach (var item in dataArray.EnumerateArray())
        {
            index++;

            // Each item has: { type, target: { ... }, ... }
            if (!item.TryGetProperty("target", out var target))
            {
                target = item;
            }

            var id = target.TryGetProperty("id", out var idEl)
                ? idEl.GetInt64()
                : 0L;

            // Try titleArea (hot-list-web format), then fallback to "title"
            string? title = null;
            if (target.TryGetProperty("titleArea", out var titleArea) &&
                titleArea.TryGetProperty("text", out var titleText))
            {
                title = titleText.GetString();
            }
            else if (target.TryGetProperty("title", out var directTitle))
            {
                title = directTitle.GetString();
            }

            if (string.IsNullOrWhiteSpace(title)) continue;

            // Description / excerpt
            string? description = null;
            if (target.TryGetProperty("excerptArea", out var excerptArea) &&
                excerptArea.TryGetProperty("text", out var excerptText))
            {
                description = excerptText.GetString();
            }
            else if (target.TryGetProperty("excerpt", out var excerpt))
            {
                description = excerpt.GetString();
            }

            // Hot metrics
            string? hotMetrics = null;
            if (target.TryGetProperty("metricsArea", out var metricsArea) &&
                metricsArea.TryGetProperty("text", out var metricsText))
            {
                hotMetrics = metricsText.GetString();
            }
            else if (target.TryGetProperty("hot", out var hot))
            {
                hotMetrics = hot.GetString();
            }

            // Image URL
            string? imageUrl = null;
            if (target.TryGetProperty("imageArea", out var imageArea) &&
                imageArea.TryGetProperty("url", out var imageUrlEl))
            {
                imageUrl = imageUrlEl.GetString();
            }

            // Link URL
            string? url = null;
            if (target.TryGetProperty("link", out var link) &&
                link.TryGetProperty("url", out var linkUrl))
            {
                url = linkUrl.GetString();
            }
            else
            {
                url = id > 0 ? $"https://www.zhihu.com/question/{id}" : null;
            }

            if (string.IsNullOrWhiteSpace(url)) continue;

            items.Add(new ZhihuHotItem
            {
                Rank = index,
                Title = title,
                Description = description,
                HotMetrics = hotMetrics,
                ImageUrl = imageUrl,
                Url = url,
                QuestionId = id
            });
        }

        return items;
    }

    /// <summary>
    /// Parses Zhihu hot list items from the HTML page.
    /// Fallback method: extract the js-initialData JSON embedded script,
    /// then parse initialState.topstory.hotList[].
    /// </summary>
    private static List<ZhihuHotItem> ParseFromHtml(string html)
    {
        var markerStart = html.IndexOf(JsonDataMarker, StringComparison.Ordinal);
        if (markerStart < 0)
        {
            Debug.WriteLine("[ZhihuHotListService] js-initialData marker not found in HTML.");
            return [];
        }

        var jsonStart = html.IndexOf('>', markerStart);
        if (jsonStart < 0) return [];

        var jsonEnd = html.IndexOf("</script>", jsonStart, StringComparison.Ordinal);
        if (jsonEnd < 0) return [];

        var rawJson = html[(jsonStart + 1)..jsonEnd].Trim();
        if (string.IsNullOrWhiteSpace(rawJson)) return [];

        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;

        if (root.TryGetProperty("initialState", out var initialState) &&
            initialState.TryGetProperty("topstory", out var topStory) &&
            topStory.TryGetProperty("hotList", out var hotList) &&
            hotList.ValueKind == JsonValueKind.Array)
        {
            var items = new List<ZhihuHotItem>(hotList.GetArrayLength());
            var index = 0;

            foreach (var item in hotList.EnumerateArray())
            {
                index++;
                if (!item.TryGetProperty("target", out var target)) continue;

                var id = target.TryGetProperty("id", out var idEl)
                    ? idEl.GetInt64()
                    : 0L;

                var title = target.TryGetProperty("titleArea", out var titleArea) &&
                            titleArea.TryGetProperty("text", out var titleText)
                    ? titleText.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(title)) continue;

                var description = target.TryGetProperty("excerptArea", out var excerptArea) &&
                                  excerptArea.TryGetProperty("text", out var excerptText)
                    ? excerptText.GetString()
                    : null;

                var hotMetrics = target.TryGetProperty("metricsArea", out var metricsArea) &&
                                 metricsArea.TryGetProperty("text", out var metricsText)
                    ? metricsText.GetString()
                    : null;

                var imageUrl = target.TryGetProperty("imageArea", out var imageArea) &&
                               imageArea.TryGetProperty("url", out var imageUrlEl)
                    ? imageUrlEl.GetString()
                    : null;

                var url = target.TryGetProperty("link", out var link) &&
                          link.TryGetProperty("url", out var linkUrl)
                    ? linkUrl.GetString()
                    : $"https://www.zhihu.com/question/{id}";

                items.Add(new ZhihuHotItem
                {
                    Rank = index,
                    Title = title,
                    Description = description,
                    HotMetrics = hotMetrics,
                    ImageUrl = imageUrl,
                    Url = url!,
                    QuestionId = id
                });
            }

            return items;
        }

        Debug.WriteLine("[ZhihuHotListService] Could not find hotList array in the HTML embedded JSON.");
        return [];
    }
}