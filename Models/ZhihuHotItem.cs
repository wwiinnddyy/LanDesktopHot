namespace LanDesktopHot.Models;

/// <summary>
/// Represents a single item from the Zhihu hot list.
/// </summary>
public sealed record ZhihuHotItem
{
    /// <summary>
    /// Ranking position on the hot list (1-based).
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Title of the hot topic / question.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Brief description or excerpt of the question.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Hot metrics text, e.g. "1000 万热度" or "3 亿热度".
    /// </summary>
    public string? HotMetrics { get; init; }

    /// <summary>
    /// URL to the thumbnail / cover image.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Full URL to the Zhihu question page.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Zhihu question ID.
    /// </summary>
    public required long QuestionId { get; init; }
}