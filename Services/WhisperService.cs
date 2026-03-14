using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutomationContent.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace AutomationContent.Services;

public class WhisperService
{
    private readonly string _ffmpegPath;
    private static readonly string ModelsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutomationContent", "models"
    );

    public WhisperService()
    {
        _ffmpegPath = FindFfmpeg();
        Directory.CreateDirectory(ModelsFolder);
    }

    public event Action<string>? StatusChanged;

    private static string FindFfmpeg()
    {
        var candidates = new[]
        {
            "ffmpeg",
            "/usr/local/bin/ffmpeg",
            "/opt/homebrew/bin/ffmpeg",
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

        return "ffmpeg";
    }

    /// <summary>
    /// Get the path to the Whisper model file, downloading if necessary.
    /// Uses "base" model (~150MB) — good balance of speed and accuracy.
    /// </summary>
    public async Task<string> EnsureModelAsync(CancellationToken ct = default)
    {
        var modelPath = Path.Combine(ModelsFolder, "ggml-base.bin");

        if (File.Exists(modelPath))
        {
            StatusChanged?.Invoke("Whisper model ready.");
            return modelPath;
        }

        StatusChanged?.Invoke("Downloading Whisper model (~150MB)... This only happens once.");

        using var modelStream = await WhisperGgmlDownloader.Default
            .GetGgmlModelAsync(GgmlType.Base, cancellationToken: ct);

        using var fileWriter = File.OpenWrite(modelPath);
        await modelStream.CopyToAsync(fileWriter, ct);

        StatusChanged?.Invoke("Whisper model downloaded successfully!");
        return modelPath;
    }

    /// <summary>
    /// Extract audio from video file to a temporary 16kHz mono WAV file.
    /// </summary>
    public async Task<string> ExtractAudioAsync(string inputFilePath, CancellationToken ct = default)
    {
        StatusChanged?.Invoke("Extracting audio from video...");

        var tempAudioPath = Path.Combine(
            Path.GetTempPath(),
            $"whisper_audio_{Guid.NewGuid():N}.wav"
        );

        // Convert to 16kHz mono 16-bit PCM WAV (required by Whisper)
        var args = $"-i \"{inputFilePath}\" -ar 16000 -ac 1 -c:a pcm_s16le -y \"{tempAudioPath}\"";

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

        if (process.ExitCode != 0 || !File.Exists(tempAudioPath))
        {
            throw new Exception("Failed to extract audio from the file. Please make sure FFmpeg is installed.");
        }

        return tempAudioPath;
    }

    /// <summary>
    /// Transcribe an audio file using Whisper.net (local inference, no API key needed).
    /// The audio must be 16kHz mono WAV.
    /// </summary>
    public async Task<TranscriptResult> TranscribeAsync(string audioFilePath, CancellationToken ct = default)
    {
        StatusChanged?.Invoke("Đang nhận dạng giọng nói...");

        // Ensure model is downloaded
        var modelPath = await EnsureModelAsync(ct);

        StatusChanged?.Invoke("Đang nhận dạng giọng nói...");

        var result = new TranscriptResult();
        var allTexts = new List<string>();

        // Create Whisper processor
        using var whisperFactory = WhisperFactory.FromPath(modelPath);

        using var processor = whisperFactory.CreateBuilder()
            .WithLanguageDetection()  // Auto-detect language
            .Build();

        await using var fileStream = File.OpenRead(audioFilePath);

        await foreach (var segment in processor.ProcessAsync(fileStream, ct))
        {
            result.Segments.Add(new TranscriptSegment
            {
                StartSeconds = segment.Start.TotalSeconds,
                EndSeconds = segment.End.TotalSeconds,
                Text = segment.Text
            });

            allTexts.Add(segment.Text.Trim());
        }

        result.FullText = string.Join(" ", allTexts);
        result.DetectedLanguage = "auto-detected";

        // Estimate duration from last segment
        if (result.Segments.Count > 0)
        {
            result.DurationSeconds = result.Segments[^1].EndSeconds;
        }

        StatusChanged?.Invoke("Transcription complete!");

        return result;
    }

    /// <summary>
    /// Check if a file is a supported media file for transcription.
    /// </summary>
    public static bool IsSupportedMediaFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".mp4" or ".mp3" or ".wav" or ".m4a" or ".webm" or ".ogg" or ".flac";
    }

    /// <summary>
    /// Determines if the file needs audio extraction (video files) or can be sent directly.
    /// Even audio files may need conversion to 16kHz mono WAV.
    /// </summary>
    public static bool NeedsAudioExtraction(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        // All non-WAV files need extraction/conversion to 16kHz mono WAV
        return ext is not ".wav";
    }
}
