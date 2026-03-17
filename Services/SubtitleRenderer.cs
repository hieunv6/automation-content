using System;
using System.IO;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AutomationContent.Models;

namespace AutomationContent.Services;

/// <summary>
/// Controls how subtitle text is displayed on images.
/// </summary>
public enum SubtitleDisplayMode
{
    /// <summary>Show the entire paragraph text (may be long)</summary>
    FullText,
    /// <summary>Show one sentence at a time</summary>
    SentenceBySentence,
    /// <summary>Limit to short chunks (~80 chars)</summary>
    ShortLines,
    /// <summary>Progressive word-by-word reveal (sync with voice)</summary>
    WordByWord
}

public static class SubtitleTextSplitter
{
    public static List<string> SplitText(string text, SubtitleDisplayMode mode)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string> { "" };

        return mode switch
        {
            SubtitleDisplayMode.FullText => new List<string> { text.Trim() },
            SubtitleDisplayMode.SentenceBySentence => SplitIntoSentences(text),
            SubtitleDisplayMode.ShortLines => SplitIntoShortChunks(text, 80),
            SubtitleDisplayMode.WordByWord => SplitIntoWordGroups(text, 4),
            _ => new List<string> { text.Trim() }
        };
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = Regex.Split(text.Trim(), @"(?<=[.!?。！？])\s+")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (sentences.Count == 0) sentences.Add(text.Trim());
        return sentences;
    }

    private static List<string> SplitIntoShortChunks(string text, int maxChars)
    {
        var chunks = new List<string>();
        var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        foreach (var word in words)
        {
            var test = string.IsNullOrEmpty(current) ? word : current + " " + word;
            if (test.Length > maxChars && !string.IsNullOrEmpty(current))
            {
                chunks.Add(current);
                current = word;
            }
            else current = test;
        }
        if (!string.IsNullOrEmpty(current)) chunks.Add(current);
        if (chunks.Count == 0) chunks.Add(text.Trim());
        return chunks;
    }

    private static List<string> SplitIntoWordGroups(string text, int wordsPerGroup)
    {
        var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        for (int i = 0; i < words.Length; i += wordsPerGroup)
        {
            var groupEnd = Math.Min(i + wordsPerGroup, words.Length);
            chunks.Add(string.Join(" ", words[i..groupEnd]));
        }
        if (chunks.Count == 0) chunks.Add(text.Trim());
        return chunks;
    }
}

public static class SubtitleRenderer
{
    private static readonly SKTypeface _typeface = SKTypeface.FromFamilyName(
        "Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

    /// <summary>
    /// Parse hex color string to SKColor. Supports #RRGGBB and #AARRGGBB.
    /// </summary>
    private static SKColor ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return SKColors.White;
        hex = hex.TrimStart('#');
        try
        {
            if (hex.Length == 6)
                return new SKColor(
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
            if (hex.Length == 8)
                return new SKColor(
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16),
                    Convert.ToByte(hex[6..8], 16),
                    Convert.ToByte(hex[..2], 16));
        }
        catch { }
        return SKColors.White;
    }

    /// <summary>
    /// Generate a preview frame (Bitmap) with subtitle burned in — for real-time preview.
    /// Does NOT save to disk. Returns SKBitmap that caller must dispose.
    /// </summary>
    public static SKBitmap? GeneratePreviewFrame(
        string inputImagePath, string displayText,
        int targetWidth, int targetHeight,
        SubtitleStyle? style = null)
    {
        SKBitmap? srcBitmap;
        try
        {
            srcBitmap = SKBitmap.Decode(inputImagePath);
        }
        catch { return null; }

        if (srcBitmap == null) return null;

        var result = new SKBitmap(targetWidth, targetHeight);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);

        // Draw the source image scaled to fit
        float scaleX = (float)targetWidth / srcBitmap.Width;
        float scaleY = (float)targetHeight / srcBitmap.Height;
        float scale = Math.Min(scaleX, scaleY);
        int drawWidth = (int)(srcBitmap.Width * scale);
        int drawHeight = (int)(srcBitmap.Height * scale);
        int dx = (targetWidth - drawWidth) / 2;
        int dy = (targetHeight - drawHeight) / 2;

        using (var paint = new SKPaint { IsAntialias = true })
        {
            canvas.DrawBitmap(srcBitmap, new SKRect(dx, dy, dx + drawWidth, dy + drawHeight), paint);
        }
        srcBitmap.Dispose();

        // Draw subtitle
        if (!string.IsNullOrWhiteSpace(displayText))
        {
            DrawStyledSubtitles(canvas, displayText, targetWidth, targetHeight, style);
        }

        return result;
    }

    /// <summary>
    /// Render subtitled image to disk with display mode and style support.
    /// </summary>
    public static string RenderSubtitledImage(
        string inputImagePath, string text,
        int targetWidth, int targetHeight, string outputDir,
        SubtitleDisplayMode mode = SubtitleDisplayMode.FullText, int subIndex = -1,
        SubtitleStyle? style = null)
    {
        string displayText;
        if (mode == SubtitleDisplayMode.FullText || subIndex < 0)
            displayText = text;
        else
        {
            var chunks = SubtitleTextSplitter.SplitText(text, mode);
            displayText = (subIndex < chunks.Count) ? chunks[subIndex] : chunks.LastOrDefault() ?? text;
        }

        var outputFileName = $"{Guid.NewGuid():N}.jpg";
        var outputPath = Path.Combine(outputDir, outputFileName);

        using var srcBitmap = SKBitmap.Decode(inputImagePath);
        if (srcBitmap == null)
            throw new Exception($"Failed to decode image at path: {inputImagePath}");

        using var surface = SKSurface.Create(new SKImageInfo(targetWidth, targetHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        float scaleX = (float)targetWidth / srcBitmap.Width;
        float scaleY = (float)targetHeight / srcBitmap.Height;
        float scale = Math.Min(scaleX, scaleY);
        int drawWidth = (int)(srcBitmap.Width * scale);
        int drawHeight = (int)(srcBitmap.Height * scale);
        int dx = (targetWidth - drawWidth) / 2;
        int dy = (targetHeight - drawHeight) / 2;

        using (var paint = new SKPaint { IsAntialias = true })
        {
            canvas.DrawBitmap(srcBitmap, new SKRect(dx, dy, dx + drawWidth, dy + drawHeight), paint);
        }

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            DrawStyledSubtitles(canvas, displayText, targetWidth, targetHeight, style);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    /// <summary>
    /// Draw subtitles with full style support.
    /// </summary>
    private static void DrawStyledSubtitles(SKCanvas canvas, string text, int w, int h, SubtitleStyle? style)
    {
        style ??= new SubtitleStyle(); // defaults

        float fontSize = style.FontSize;
        using var font = new SKFont(_typeface, fontSize);

        var fontColor = ParseColor(style.FontColor);
        var outlineColor = ParseColor(style.OutlineColor);

        using var textPaint = new SKPaint { Color = fontColor, IsAntialias = true };
        using var strokePaint = new SKPaint
        {
            Color = outlineColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = style.OutlineWidth,
            StrokeJoin = SKStrokeJoin.Round
        };

        var wrappedLines = WrapText(font, text, w * 0.9f);

        // Limit to max 3 lines
        if (wrappedLines.Count > 3)
        {
            wrappedLines = wrappedLines.Take(3).ToList();
            var last = wrappedLines[2];
            if (last.Length > 3) wrappedLines[2] = last[..^3] + "...";
        }

        float lineHeight = fontSize * 1.3f;
        float totalTextHeight = wrappedLines.Count * lineHeight;
        float margin = style.VerticalMargin;

        // Calculate Y position based on position setting
        float startY;
        switch (style.Position)
        {
            case SubtitlePosition.Top:
                startY = margin + lineHeight; // first line baseline
                break;
            case SubtitlePosition.Center:
                startY = (h - totalTextHeight) / 2f + lineHeight;
                break;
            default: // Bottom
                startY = h - margin - totalTextHeight + lineHeight;
                break;
        }

        float centerX = w / 2f;

        // Draw background box if enabled
        if (style.ShowBackground)
        {
            var bgColor = ParseColor(style.BackgroundColor);
            using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };

            float maxLineWidth = 0;
            foreach (var line in wrappedLines)
            {
                var lw = font.MeasureText(line);
                if (lw > maxLineWidth) maxLineWidth = lw;
            }

            float padX = 20f, padY = 10f;
            float bgX = centerX - maxLineWidth / 2f - padX;
            float bgY = startY - lineHeight + padY / 2f;
            float bgW = maxLineWidth + padX * 2;
            float bgH = totalTextHeight + padY;

            canvas.DrawRoundRect(bgX, bgY, bgW, bgH, 8, 8, bgPaint);
        }

        for (int i = 0; i < wrappedLines.Count; i++)
        {
            float y = startY + i * lineHeight;
            if (style.OutlineWidth > 0)
                canvas.DrawText(wrappedLines[i], centerX, y, SKTextAlign.Center, font, strokePaint);
            canvas.DrawText(wrappedLines[i], centerX, y, SKTextAlign.Center, font, textPaint);
        }
    }

    private static List<string> WrapText(SKFont font, string text, float maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string currentLine = "";

        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(currentLine))
                currentLine = word;
            else
            {
                string testLine = currentLine + " " + word;
                if (font.MeasureText(testLine) > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else currentLine = testLine;
            }
        }
        if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);
        return lines;
    }
}
