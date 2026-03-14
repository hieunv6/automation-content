namespace AutomationContent.Models;

public class VideoInfo
{
    public string Title { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public string UploadDate { get; set; } = string.Empty;

    public string FormattedDuration
    {
        get
        {
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return ts.Hours > 0
                ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }

    public string FormattedViewCount
    {
        get
        {
            if (ViewCount >= 1_000_000_000) return $"{ViewCount / 1_000_000_000.0:F1}B views";
            if (ViewCount >= 1_000_000) return $"{ViewCount / 1_000_000.0:F1}M views";
            if (ViewCount >= 1_000) return $"{ViewCount / 1_000.0:F1}K views";
            return $"{ViewCount:N0} views";
        }
    }
}
