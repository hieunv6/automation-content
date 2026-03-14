using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationContent.Services;

/// <summary>
/// Wraps the edge-tts CLI tool for free text-to-speech using Microsoft Edge voices.
/// Install: pip install edge-tts
/// </summary>
public class EdgeTtsService
{
    private readonly string _edgeTtsPath;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private Process? _currentProcess;

    public event Action<string>? StatusChanged;

    public EdgeTtsService()
    {
        _edgeTtsPath = FindEdgeTts();
        _ffmpegPath = FindTool("ffmpeg");
        _ffprobePath = FindTool("ffprobe");
    }

    /// <summary>
    /// Check if edge-tts is installed and runnable.
    /// </summary>
    public bool IsEdgeTtsInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _edgeTtsPath,
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

    /// <summary>
    /// Generate speech from a list of paragraphs using edge-tts.
    /// Returns the path to the merged audio file and a list of exact durations for each paragraph chunk.
    /// </summary>
    public async Task<(string outputPath, List<double> chunkDurations)> GenerateVoiceoverFromParagraphsAsync(
        List<string> paragraphs,
        string voice,
        string rate,
        string outputFolder,
        string baseName,
        Action<int, int>? progressCallback = null,
        CancellationToken ct = default)
    {
        if (paragraphs == null || paragraphs.Count == 0)
            throw new Exception("Không có nội dung để tạo giọng nói.");

        // Clean text: remove timestamps
        var cleanedParagraphs = paragraphs
            .Select(p => Regex.Replace(p, @"\[\d{1,2}:\d{2}(:\d{2})?\]\s*", "").Trim())
            .ToList();

        if (cleanedParagraphs.All(string.IsNullOrWhiteSpace))
            throw new Exception("Nội dung chỉ chứa timestamps. Vui lòng thêm nội dung text.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"edge_tts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var chunkFiles = new List<string>();
        var chunkDurations = new List<double>();

        try
        {
            // Generate each chunk
            for (int i = 0; i < cleanedParagraphs.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var pText = cleanedParagraphs[i];
                var chunkFile = Path.Combine(tempDir, $"chunk_{i:D4}.mp3");

                StatusChanged?.Invoke($"Đang tạo giọng nói... (đoạn {i + 1}/{cleanedParagraphs.Count})");
                progressCallback?.Invoke(i + 1, cleanedParagraphs.Count);

                double duration = 0;
                if (!string.IsNullOrWhiteSpace(pText))
                {
                    await GenerateChunkAsync(pText, voice, rate, chunkFile, ct);
                    if (File.Exists(chunkFile) && new FileInfo(chunkFile).Length > 0)
                    {
                        chunkFiles.Add(chunkFile);
                        duration = await GetAudioDurationAsync(chunkFile, ct);
                    }
                    else
                    {
                        throw new Exception($"Lỗi tạo giọng nói cho đoạn {i + 1}. Vui lòng kiểm tra kết nối mạng.");
                    }
                }
                
                // Track duration (0 if paragraph was empty, to maintain index alignment)
                chunkDurations.Add(Math.Round(duration, 2));
            }

            if (chunkFiles.Count == 0)
                throw new Exception("Không tạo được bất kỳ đoạn âm thanh nào.");

            // Output path
            var outputFileName = SanitizeFileName(baseName) + "_voiceover.mp3";
            var outputPath = Path.Combine(outputFolder, outputFileName);

            // Avoid overwriting
            var counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(outputFolder, SanitizeFileName(baseName) + $"_voiceover_{counter}.mp3");
                counter++;
            }

            // Merge if multiple chunks, otherwise just copy
            if (chunkFiles.Count == 1)
            {
                File.Copy(chunkFiles[0], outputPath, true);
            }
            else
            {
                StatusChanged?.Invoke("Đang ghép các đoạn âm thanh...");
                await MergeAudioFilesAsync(chunkFiles, outputPath, ct);
            }

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                throw new Exception("Lỗi khi ghép file âm thanh. Vui lòng thử lại.");
            }

            StatusChanged?.Invoke("✅ Lồng tiếng hoàn tất!");
            return (outputPath, chunkDurations);
        }
        finally
        {
            // Cleanup temp files
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }

    /// <summary>
    /// Generate speech from text using edge-tts.
    /// Splits text into chunks, generates audio for each, then merges with ffmpeg.
    /// </summary>
    public async Task<string> GenerateVoiceoverAsync(
        string text,
        string voice,
        string rate,
        string outputFolder,
        string baseName,
        Action<int, int>? progressCallback = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new Exception("Không có nội dung để tạo giọng nói.");

        // Clean text: remove timestamps like [00:00], [01:23:45]
        var cleanedText = Regex.Replace(text, @"\[\d{1,2}:\d{2}(:\d{2})?\]\s*", "");
        cleanedText = cleanedText.Trim();

        if (string.IsNullOrWhiteSpace(cleanedText))
            throw new Exception("Nội dung chỉ chứa timestamps. Vui lòng thêm nội dung text.");

        // Split into chunks
        var chunks = SplitIntoChunks(cleanedText, 300);
        if (chunks.Count == 0)
            throw new Exception("Không thể phân đoạn nội dung.");

        var (outputPath, _) = await GenerateVoiceoverFromParagraphsAsync(
            chunks, voice, rate, outputFolder, baseName, progressCallback, ct);
            
        return outputPath;
    }

    /// <summary>
    /// Generate a single chunk of audio using edge-tts.
    /// </summary>
    private async Task GenerateChunkAsync(string text, string voice, string rate, string outputPath, CancellationToken ct)
    {
        // Escape text for command line - write to temp file to avoid shell escaping issues
        var textFile = Path.Combine(Path.GetDirectoryName(outputPath)!, $"text_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(textFile, text, ct);

        try
        {
            // Build args: edge-tts --voice "vi-VN-HoaiMyNeural" --rate "+0%" -f textfile --write-media output.mp3
            var args = $"--voice \"{voice}\" --rate \"{rate}\" -f \"{textFile}\" --write-media \"{outputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = _edgeTtsPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            _currentProcess = process;
            process.Start();

            // Read stderr for error detection
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                if (stderr.Contains("connect", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("ConnectionError", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Edge TTS cần kết nối internet. Vui lòng kiểm tra mạng.");
                }
                throw new Exception($"Edge TTS lỗi: {stderr}");
            }
        }
        finally
        {
            _currentProcess = null;
            try { if (File.Exists(textFile)) File.Delete(textFile); } catch { }
        }
    }

    /// <summary>
    /// Get the duration of an audio file in seconds using ffprobe.
    /// </summary>
    private async Task<double> GetAudioDurationAsync(string audioPath, CancellationToken ct)
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

        if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var duration))
        {
            return duration;
        }

        return 0;
    }

    /// <summary>
    /// Merge multiple audio files into one using ffmpeg concat filter.
    /// </summary>
    private async Task MergeAudioFilesAsync(List<string> inputFiles, string outputPath, CancellationToken ct)
    {
        // Create concat list file for ffmpeg
        var listFile = Path.Combine(Path.GetDirectoryName(inputFiles[0])!, "concat_list.txt");
        var lines = inputFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'");
        await File.WriteAllLinesAsync(listFile, lines, ct);

        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c:a libmp3lame -q:a 2 \"{outputPath}\"";

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
        process.Start();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new Exception($"FFmpeg merge failed: {stderr}");
        }
    }

    /// <summary>
    /// Split text into chunks of max N characters at sentence boundaries.
    /// </summary>
    public static List<string> SplitIntoChunks(string text, int maxChars = 300)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        // Split by sentence-ending punctuation (keep the punctuation)
        var sentences = Regex.Split(text, @"(?<=[.!?。！？\n])\s*")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var currentChunk = "";
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // If single sentence is longer than max, split by commas or force-split
            if (trimmed.Length > maxChars)
            {
                // Flush current
                if (!string.IsNullOrWhiteSpace(currentChunk))
                {
                    chunks.Add(currentChunk.Trim());
                    currentChunk = "";
                }
                // Split long sentence by commas
                var subParts = Regex.Split(trimmed, @"(?<=[,;，；])\s*")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                var subChunk = "";
                foreach (var part in subParts)
                {
                    if ((subChunk + " " + part).Trim().Length > maxChars)
                    {
                        if (!string.IsNullOrWhiteSpace(subChunk))
                            chunks.Add(subChunk.Trim());
                        subChunk = part;
                    }
                    else
                    {
                        subChunk = string.IsNullOrEmpty(subChunk) ? part : subChunk + " " + part;
                    }
                }
                if (!string.IsNullOrWhiteSpace(subChunk))
                    chunks.Add(subChunk.Trim());

                continue;
            }

            // Normal case: accumulate sentences
            if ((currentChunk + " " + trimmed).Trim().Length > maxChars)
            {
                if (!string.IsNullOrWhiteSpace(currentChunk))
                    chunks.Add(currentChunk.Trim());
                currentChunk = trimmed;
            }
            else
            {
                currentChunk = string.IsNullOrEmpty(currentChunk) ? trimmed : currentChunk + " " + trimmed;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
            chunks.Add(currentChunk.Trim());

        return chunks;
    }

    /// <summary>
    /// Cancel the current generation process.
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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        // Trim and limit length
        sanitized = sanitized.Trim().TrimEnd('.');
        if (sanitized.Length > 80)
            sanitized = sanitized[..80];
        return string.IsNullOrWhiteSpace(sanitized) ? "voiceover" : sanitized;
    }

    private static string FindEdgeTts()
    {
        var candidates = new[]
        {
            "edge-tts",
            "/usr/local/bin/edge-tts",
            "/opt/homebrew/bin/edge-tts",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin/edge-tts"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.12/bin/edge-tts"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.11/bin/edge-tts"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.10/bin/edge-tts"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Python/3.9/bin/edge-tts"),
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
            catch { }
        }

        return "edge-tts"; // fallback
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

        return toolName;
    }
}
