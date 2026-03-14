namespace AutomationContent.Models;

public enum VideoQuality
{
    Quality1080p,
    Quality720p,
    Quality480p,
    AudioOnly
}

public static class VideoQualityExtensions
{
    public static string ToDisplayString(this VideoQuality quality)
    {
        return quality switch
        {
            VideoQuality.Quality1080p => "1080p (Full HD)",
            VideoQuality.Quality720p => "720p (HD)",
            VideoQuality.Quality480p => "480p (SD)",
            VideoQuality.AudioOnly => "Audio Only (MP3)",
            _ => "720p (HD)"
        };
    }

    public static string ToYtDlpFormat(this VideoQuality quality)
    {
        // Force H.264 (avc1) + AAC (mp4a) for maximum playback compatibility.
        // Without these filters, yt-dlp picks AV1 + Opus which many players can't play
        // (QuickTime, Windows Media Player, Smart TVs, older phones).
        return quality switch
        {
            VideoQuality.Quality1080p => "bestvideo[height<=1080][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]",
            VideoQuality.Quality720p => "bestvideo[height<=720][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=720]+bestaudio/best[height<=720]",
            VideoQuality.Quality480p => "bestvideo[height<=480][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=480]+bestaudio/best[height<=480]",
            VideoQuality.AudioOnly => "bestaudio/best",
            _ => "bestvideo[height<=720][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=720]+bestaudio/best[height<=720]"
        };
    }

    public static string GetFileExtension(this VideoQuality quality)
    {
        return quality == VideoQuality.AudioOnly ? "mp3" : "mp4";
    }
}
