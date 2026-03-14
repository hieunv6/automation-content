using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutomationContent.Models;
using Newtonsoft.Json.Linq;

namespace AutomationContent.Services;

public class YtDlpService
{
    private Process? _currentProcess;
    private readonly string _ytDlpPath;

    public YtDlpService()
    {
        _ytDlpPath = FindYtDlp();
    }

    public event Action<double, string>? ProgressChanged;
    public event Action<string>? StatusChanged;

    private static string FindYtDlp()
    {
        // Try common locations
        var candidates = new[]
        {
            "yt-dlp",
            "/usr/local/bin/yt-dlp",
            "/opt/homebrew/bin/yt-dlp",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "yt-dlp"),
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(5000);
                    if (p.ExitCode == 0)
                        return candidate;
                }
            }
            catch
            {
                // Continue to next candidate
            }
        }

        return "yt-dlp"; // fallback, let it fail with a user-friendly message
    }

    public bool IsYtDlpInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }

    public static bool IsValidYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        var patterns = new[]
        {
            @"^(https?://)?(www\.)?(youtube\.com/watch\?v=)[\w-]{11}",
            @"^(https?://)?(www\.)?(youtu\.be/)[\w-]{11}",
            @"^(https?://)?(www\.)?(youtube\.com/shorts/)[\w-]{11}",
            @"^(https?://)?(m\.youtube\.com/watch\?v=)[\w-]{11}"
        };

        foreach (var pattern in patterns)
        {
            if (Regex.IsMatch(url.Trim(), pattern))
                return true;
        }

        return false;
    }

    public async Task<VideoInfo?> GetVideoInfoAsync(string url, CancellationToken ct = default)
    {
        StatusChanged?.Invoke("Fetching video information...");

        var args = $"--dump-json --no-download \"{url.Trim()}\"";
        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var errorMsg = error.ToString();
            throw new Exception(GetFriendlyError(errorMsg));
        }

        var json = output.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new Exception("Could not retrieve video information. Please check the URL and try again.");
        }

        try
        {
            var obj = JObject.Parse(json);
            return new VideoInfo
            {
                Title = obj["title"]?.ToString() ?? "Unknown Title",
                ThumbnailUrl = obj["thumbnail"]?.ToString() ?? "",
                DurationSeconds = obj["duration"]?.Value<int>() ?? 0,
                Channel = obj["channel"]?.ToString() ?? obj["uploader"]?.ToString() ?? "Unknown Channel",
                Url = url.Trim(),
                ViewCount = obj["view_count"]?.Value<long>() ?? 0,
                UploadDate = obj["upload_date"]?.ToString() ?? ""
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse video information: {ex.Message}");
        }
    }

    public async Task<string> DownloadAsync(string url, string outputFolder, VideoQuality quality,
        CancellationToken ct = default)
    {
        StatusChanged?.Invoke("Starting download...");
        ProgressChanged?.Invoke(0, "Preparing...");

        var isAudioOnly = quality == VideoQuality.AudioOnly;
        var ext = quality.GetFileExtension();

        var outputTemplate = Path.Combine(outputFolder, "%(title)s.%(ext)s");

        var args = new StringBuilder();
        args.Append($"-f \"{quality.ToYtDlpFormat()}\" ");
        args.Append($"-o \"{outputTemplate}\" ");
        args.Append("--newline ");
        args.Append("--progress ");
        args.Append("--no-mtime ");

        if (isAudioOnly)
        {
            args.Append("-x ");
            args.Append("--audio-format mp3 ");
            args.Append("--audio-quality 0 ");
        }
        else
        {
            args.Append("--merge-output-format mp4 ");
        }

        args.Append($"\"{url.Trim()}\"");

        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            Arguments = args.ToString(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _currentProcess = new Process { StartInfo = psi };
        var errorOutput = new StringBuilder();
        string? finalFilePath = null;

        _currentProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            ParseProgress(e.Data);

            // Detect the final file path
            var mergeMatch = Regex.Match(e.Data, @"\[Merger\] Merging formats into ""(.+)""");
            if (mergeMatch.Success)
            {
                finalFilePath = mergeMatch.Groups[1].Value;
            }

            var destMatch = Regex.Match(e.Data, @"\[download\] Destination: (.+)");
            if (destMatch.Success)
            {
                finalFilePath = destMatch.Groups[1].Value;
            }

            var alreadyMatch = Regex.Match(e.Data, @"\[download\] (.+) has already been downloaded");
            if (alreadyMatch.Success)
            {
                finalFilePath = alreadyMatch.Groups[1].Value;
            }

            var extractMatch = Regex.Match(e.Data, @"\[ExtractAudio\] Destination: (.+)");
            if (extractMatch.Success)
            {
                finalFilePath = extractMatch.Groups[1].Value;
            }
        };

        _currentProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorOutput.AppendLine(e.Data);
        };

        _currentProcess.Start();
        _currentProcess.BeginOutputReadLine();
        _currentProcess.BeginErrorReadLine();

        try
        {
            await _currentProcess.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            CancelDownload();
            throw;
        }

        if (_currentProcess.ExitCode != 0)
        {
            var errorMsg = errorOutput.ToString();
            throw new Exception(GetFriendlyError(errorMsg));
        }

        ProgressChanged?.Invoke(100, "Complete!");
        StatusChanged?.Invoke("Download complete!");

        // Try to find the downloaded file if we didn't capture it
        if (string.IsNullOrEmpty(finalFilePath) || !File.Exists(finalFilePath))
        {
            // Find the most recently modified file in the output folder
            var di = new DirectoryInfo(outputFolder);
            var files = di.GetFiles($"*.{ext}");
            if (files.Length > 0)
            {
                Array.Sort(files, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                finalFilePath = files[0].FullName;
            }
        }

        return finalFilePath ?? outputFolder;
    }

    public void CancelDownload()
    {
        try
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                _currentProcess.Kill(entireProcessTree: true);
            }
        }
        catch { }
    }

    private void ParseProgress(string line)
    {
        // yt-dlp progress line: [download]  45.2% of   50.00MiB at    5.00MiB/s ETA 00:06
        var match = Regex.Match(line, @"\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+\w+)\s+at\s+([\d.]+\w+/s)\s+ETA\s+(\S+)");
        if (match.Success)
        {
            if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent))
            {
                var size = match.Groups[2].Value;
                var speed = match.Groups[3].Value;
                var eta = match.Groups[4].Value;
                var status = $"{percent:F1}% of {size} • {speed} • ETA {eta}";
                ProgressChanged?.Invoke(percent, status);
            }
        }

        // Also handle simpler progress lines
        var simpleMatch = Regex.Match(line, @"\[download\]\s+([\d.]+)%");
        if (simpleMatch.Success && !match.Success)
        {
            if (double.TryParse(simpleMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent))
            {
                ProgressChanged?.Invoke(percent, $"{percent:F1}%");
            }
        }

        // Downloading status
        if (line.Contains("[download] Downloading"))
        {
            StatusChanged?.Invoke("Downloading...");
        }

        if (line.Contains("[Merger]"))
        {
            StatusChanged?.Invoke("Merging audio and video...");
            ProgressChanged?.Invoke(95, "Merging...");
        }

        if (line.Contains("[ExtractAudio]"))
        {
            StatusChanged?.Invoke("Extracting audio...");
            ProgressChanged?.Invoke(95, "Extracting audio...");
        }

        if (line.Contains("100%"))
        {
            ProgressChanged?.Invoke(100, "Finalizing...");
        }
    }

    private static string GetFriendlyError(string errorMsg)
    {
        if (string.IsNullOrWhiteSpace(errorMsg))
            return "An unknown error occurred. Please try again.";

        if (errorMsg.Contains("Video unavailable") || errorMsg.Contains("is not available"))
            return "This video is unavailable. It may have been removed, set to private, or restricted in your region.";

        if (errorMsg.Contains("Private video"))
            return "This video is private. You can only download public or unlisted videos.";

        if (errorMsg.Contains("Sign in to confirm your age"))
            return "This video requires age verification and cannot be downloaded directly.";

        if (errorMsg.Contains("is not a valid URL") || errorMsg.Contains("Unsupported URL"))
            return "The URL you entered doesn't appear to be a valid YouTube link. Please check the URL and try again.";

        if (errorMsg.Contains("Unable to download webpage") || errorMsg.Contains("urlopen error"))
            return "Could not connect to YouTube. Please check your internet connection and try again.";

        if (errorMsg.Contains("HTTP Error 429"))
            return "Too many requests. YouTube is temporarily blocking downloads. Please wait a few minutes and try again.";

        if (errorMsg.Contains("HTTP Error 403"))
            return "Access denied. YouTube is blocking this download. Try updating yt-dlp.";

        if (errorMsg.Contains("No video formats") || errorMsg.Contains("Requested format is not available"))
            return "The selected quality is not available for this video. Try a different quality setting.";

        if (errorMsg.Contains("ffmpeg") || errorMsg.Contains("FFmpeg"))
            return "FFmpeg is required but not found. Please install FFmpeg to enable video merging and audio extraction.";

        // Generic but still friendly
        return "Something went wrong while processing your request. Please check the URL and try again.\n\nIf the problem persists, try updating yt-dlp.";
    }
}
