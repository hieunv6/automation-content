using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationContent.Services;

/// <summary>
/// Renders a final MP4 video from generated images + voiceover audio using ffmpeg.
/// Runs 100% offline — no internet needed.
/// </summary>
public class VideoRenderService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private Process? _currentProcess;

    public event Action<string>? StatusChanged;
    public event Action<double, string>? ProgressChanged; // (percent, timeText)

    public VideoRenderService()
    {
        _ffmpegPath = FindTool("ffmpeg");
        _ffprobePath = FindTool("ffprobe");
    }

    /// <summary>
    /// Get the duration of an audio file in seconds using ffprobe.
    /// </summary>
    public async Task<double> GetAudioDurationAsync(string audioPath, CancellationToken ct = default)
    {
        var args = $"-i \"{audioPath}\" -show_entries format=duration -v quiet -of csv=p=0";

        var psi = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
        {
            return duration;
        }

        return 0;
    }

    /// <summary>
    /// Render a video from images + voiceover audio.
    /// </summary>
    /// <param name="imagePaths">Ordered list of image file paths</param>
    /// <param name="paragraphTexts">Subtitle text for each image (same count as imagePaths)</param>
    /// <param name="voiceoverPath">Path to the voiceover MP3 file</param>
    /// <param name="outputPath">Full path for the output MP4</param>
    /// <param name="width">Output width (1920 or 1280)</param>
    /// <param name="height">Output height (1080 or 720)</param>
    /// <param name="ct">Cancellation token</param>
    public async Task RenderVideoAsync(
        List<string> imagePaths,
        List<string> paragraphTexts,
        List<double> voiceoverChunkDurations,
        string voiceoverPath,
        string outputPath,
        int width = 1920,
        int height = 1080,
        CancellationToken ct = default)
    {
        if (imagePaths.Count == 0)
            throw new Exception("Không có ảnh nào để render video.");

        if (!File.Exists(voiceoverPath))
            throw new Exception("Không tìm thấy file voiceover.");

        if (imagePaths.Count != paragraphTexts.Count || imagePaths.Count != voiceoverChunkDurations.Count)
        {
            // If they don't match, we still try to render, but clamp to minimum
            var min = Math.Min(imagePaths.Count, Math.Min(paragraphTexts.Count, voiceoverChunkDurations.Count));
            imagePaths = imagePaths.Take(min).ToList();
            paragraphTexts = paragraphTexts.Take(min).ToList();
            voiceoverChunkDurations = voiceoverChunkDurations.Take(min).ToList();
        }

        // Clamp durations to ensure ffmpeg doesn't fail on extremely short chunks
        for (int i = 0; i < voiceoverChunkDurations.Count; i++)
        {
            if (voiceoverChunkDurations[i] < 0.5)
                voiceoverChunkDurations[i] = 0.5;
        }

        // Total audio duration based on chunks (to match the video generation exactly)
        var totalDuration = voiceoverChunkDurations.Sum();
        var crossfadeDuration = 0.3;

        // Step 1: Generate SRT subtitle file
        StatusChanged?.Invoke("Đang tạo phụ đề...");
        var tempDir = Path.Combine(Path.GetTempPath(), $"render_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var srtPath = Path.Combine(tempDir, "subtitles.srt");
        GenerateSrtFile(paragraphTexts, voiceoverChunkDurations, crossfadeDuration, srtPath);

        // Step 2: Build ffmpeg command with xfade filter
        StatusChanged?.Invoke("Đang render video...");

        try
        {
            var filterScriptPath = Path.Combine(tempDir, "filter.txt");
            var args = BuildFfmpegCommandAndFilter(imagePaths, voiceoverChunkDurations, voiceoverPath, srtPath, crossfadeDuration, width, height, filterScriptPath, outputPath);

            await RunFfmpegAsync(args, totalDuration, outputPath, ct);

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                throw new Exception("Render video thất bại — file output rỗng.");

            StatusChanged?.Invoke("✅ Render video hoàn tất!");
        }
        finally
        {
            // Cleanup
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }

    /// <summary>
    /// Builds the ffmpeg arguments and the complex filter script for xfade transitions.
    /// </summary>
    private string BuildFfmpegCommandAndFilter(
        List<string> imagePaths,
        List<double> durations,
        string voiceoverPath,
        string srtPath,
        double crossfade,
        int width,
        int height,
        string filterScriptPath,
        string outputPath)
    {
        var sbArgs = new StringBuilder();
        var sbFilter = new StringBuilder();

        sbArgs.Append("-y ");

        // Inputs: Add each image as a looping input with specific duration
        for (int i = 0; i < imagePaths.Count; i++)
        {
            // Image duration = audio duration + crossfade (except last image)
            var imgDuration = durations[i] + (i < imagePaths.Count - 1 ? crossfade : 0);
            
            sbArgs.Append($"-loop 1 -framerate 30 -t {imgDuration.ToString("F3", CultureInfo.InvariantCulture)} -i \"{imagePaths[i]}\" ");
        }

        // Add voiceover audio input
        var audioInputIndex = imagePaths.Count;
        sbArgs.Append($"-i \"{voiceoverPath}\" ");

        // Build filter script
        // 1. Scale and pad all images to target resolution and frame rate
        for (int i = 0; i < imagePaths.Count; i++)
        {
            sbFilter.Append($"[{i}:v]scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,fps=30,format=yuv420p[v{i}];\n");
        }

        // 2. Apply xfade transitions
        if (imagePaths.Count == 1)
        {
            // No transitions
            sbFilter.Append($"[v0]copy[outv_raw];\n");
        }
        else
        {
            var currentOffset = 0.0;
            for (int i = 0; i < imagePaths.Count - 1; i++)
            {
                currentOffset += durations[i];
                var in1 = i == 0 ? "[v0]" : $"[x{i}]";
                var in2 = $"[v{i + 1}]";
                var outNode = $"[x{i + 1}]";
                var offsetStr = currentOffset.ToString("F3", CultureInfo.InvariantCulture);
                var durationStr = crossfade.ToString("F2", CultureInfo.InvariantCulture);

                sbFilter.Append($"{in1}{in2}xfade=transition=fade:duration={durationStr}:offset={offsetStr}{outNode};\n");
            }
            sbFilter.Append($"[x{imagePaths.Count - 1}]copy[outv_raw];\n");
        }

        // 3. Apply subtitles
        var escapedSrt = srtPath.Replace("\\", "/").Replace(":", "\\\\:");
        var subtitleFilter = $"subtitles='{escapedSrt}':force_style='FontName=Arial,FontSize=36," +
                             $"PrimaryColour=&HFFFFFF&,OutlineColour=&H000000&,Outline=2," +
                             $"Alignment=2,MarginV=40'";
        sbFilter.Append($"[outv_raw]{subtitleFilter}[outv]");

        File.WriteAllText(filterScriptPath, sbFilter.ToString());

        sbArgs.Append($"-filter_complex_script \"{filterScriptPath}\" ");
        sbArgs.Append($"-map \"[outv]\" -map {audioInputIndex}:a ");
        sbArgs.Append($"-c:v libx264 -preset medium -crf 23 ");
        sbArgs.Append($"-c:a aac -b:a 192k ");
        sbArgs.Append($"-shortest -movflags +faststart ");
        sbArgs.Append($"\"{outputPath}\"");

        return sbArgs.ToString();
    }

    /// <summary>
    /// Generate SRT subtitle file from paragraph texts and durations.
    /// Auto line-break at 60 characters.
    /// </summary>
    private void GenerateSrtFile(List<string> paragraphs, List<double> durations, double crossfade, string outputPath)
    {
        var sb = new StringBuilder();
        double currentTime = 0;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var startTime = currentTime;
            var endTime = currentTime + durations[i];
            if (i < paragraphs.Count - 1)
            {
                // Subtract half crossfade from end to avoid subtitle overlap during transition
                endTime -= crossfade / 2;
            }

            // Format SRT timestamps
            var start = FormatSrtTime(startTime);
            var end = FormatSrtTime(endTime);

            // Auto line-break at 60 characters
            var text = WrapText(paragraphs[i], 60);

            sb.AppendLine($"{i + 1}");
            sb.AppendLine($"{start} --> {end}");
            sb.AppendLine(text);
            sb.AppendLine();

            // Advance time, accounting for crossfade overlap with next image
            currentTime = endTime - (i < paragraphs.Count - 1 ? crossfade / 2 : 0);
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }



    /// <summary>
    /// Run ffmpeg with progress parsing.
    /// </summary>
    private async Task RunFfmpegAsync(string args, double totalDuration, string outputPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        _currentProcess = process;

        process.Start();

        // Parse ffmpeg stderr for progress updates
        var stderrTask = Task.Run(async () =>
        {
            var buffer = new char[4096];
            var lineBuffer = new StringBuilder();

            while (!process.StandardError.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                var read = await process.StandardError.ReadAsync(buffer, ct);
                if (read > 0)
                {
                    lineBuffer.Append(buffer, 0, read);
                    var text = lineBuffer.ToString();

                    // Look for time= in ffmpeg output
                    var timeMatch = Regex.Match(text, @"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");
                    if (timeMatch.Success)
                    {
                        var hours = int.Parse(timeMatch.Groups[1].Value);
                        var minutes = int.Parse(timeMatch.Groups[2].Value);
                        var seconds = int.Parse(timeMatch.Groups[3].Value);
                        var centiseconds = int.Parse(timeMatch.Groups[4].Value);

                        var currentSeconds = hours * 3600 + minutes * 60 + seconds + centiseconds / 100.0;
                        var percent = totalDuration > 0 ? (currentSeconds / totalDuration) * 100 : 0;
                        percent = Math.Min(percent, 99);

                        var currentFormatted = FormatTime(currentSeconds);
                        var totalFormatted = FormatTime(totalDuration);
                        var progressText = $"Đang render... {currentFormatted} / {totalFormatted}";

                        ProgressChanged?.Invoke(percent, progressText);
                    }

                    // Keep only the last part to avoid memory growth
                    if (lineBuffer.Length > 8192)
                    {
                        lineBuffer.Remove(0, lineBuffer.Length - 2048);
                    }
                }
            }
        }, ct);

        try
        {
            await process.WaitForExitAsync(ct);
            await stderrTask;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }
            throw;
        }
        finally
        {
            _currentProcess = null;
        }

        if (process.ExitCode != 0)
        {
            // Try to read remaining stderr for error message
            var remaining = "";
            try { remaining = await process.StandardError.ReadToEndAsync(ct); } catch { }
            
            var logPath = outputPath + ".error.log";
            try 
            {
                var errorLog = $"========================================\n" +
                               $"FFMPEG ERROR LOG\n" +
                               $"========================================\n" +
                               $"EXIT CODE: {process.ExitCode}\n" +
                               $"----------------------------------------\n" +
                               $"COMMAND LINE ARGUMENTS:\n" +
                               $"{args}\n" +
                               $"----------------------------------------\n" +
                               $"ERROR OUTPUT (STDERR):\n" +
                               $"{remaining}\n" +
                               $"========================================";
                File.WriteAllText(logPath, errorLog);
            }
            catch { }

            throw new Exception($"FFmpeg lỗi (exit code {process.ExitCode}). Cụ thể xem file log: {Path.GetFileName(logPath)}");
        }
    }

    /// <summary>
    /// Cancel the current render process.
    /// </summary>
    public void Cancel()
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

    /// <summary>
    /// Get video info (duration, file size) using ffprobe.
    /// </summary>
    public async Task<(double duration, long fileSize)> GetVideoInfoAsync(string videoPath, CancellationToken ct = default)
    {
        double duration = 0;
        long fileSize = 0;

        if (File.Exists(videoPath))
        {
            fileSize = new FileInfo(videoPath).Length;
            duration = await GetAudioDurationAsync(videoPath, ct); // ffprobe works for video too
        }

        return (duration, fileSize);
    }

    /// <summary>
    /// Format file size in human-readable format.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    // ─── Helpers ───

    private static string FormatSrtTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static string WrapText(string text, int maxLineLength)
    {
        if (text.Length <= maxLineLength)
            return text;

        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            if ((currentLine + " " + word).Trim().Length > maxLineLength)
            {
                if (!string.IsNullOrWhiteSpace(currentLine))
                    lines.Add(currentLine.Trim());
                currentLine = word;
            }
            else
            {
                currentLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentLine))
            lines.Add(currentLine.Trim());

        return string.Join("\n", lines);
    }

    private static string FindTool(string toolName)
    {
        var candidates = new[]
        {
            toolName,
            $"/usr/local/bin/{toolName}",
            $"/opt/homebrew/bin/{toolName}",
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-version",
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
            catch { }
        }

        return toolName; // fallback
    }
}
