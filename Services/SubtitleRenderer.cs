using System;
using System.IO;
using SkiaSharp;
using System.Collections.Generic;

namespace AutomationContent.Services;

public static class SubtitleRenderer
{
    private static readonly SKTypeface _typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

    /// <summary>
    /// Resizes the image to fit within the target dimensions (padding with black to maintain aspect ratio)
    /// and burns the provided subtitle text onto the image.
    /// </summary>
    public static string RenderSubtitledImage(string inputImagePath, string text, int targetWidth, int targetHeight, string outputDir)
    {
        var outputFileName = $"{Guid.NewGuid():N}.jpg";
        var outputPath = Path.Combine(outputDir, outputFileName);

        using var srcBitmap = SKBitmap.Decode(inputImagePath);
        if (srcBitmap == null)
            throw new Exception($"Failed to decode image at path: {inputImagePath}");

        using var surface = SKSurface.Create(new SKImageInfo(targetWidth, targetHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        // 1. Calculate proper scale to maintain aspect ratio
        float scaleX = (float)targetWidth / srcBitmap.Width;
        float scaleY = (float)targetHeight / srcBitmap.Height;
        float scale = Math.Min(scaleX, scaleY);

        int drawWidth = (int)(srcBitmap.Width * scale);
        int drawHeight = (int)(srcBitmap.Height * scale);

        // Calculate offsets to center the image
        int dx = (targetWidth - drawWidth) / 2;
        int dy = (targetHeight - drawHeight) / 2;

        var destRect = new SKRect(dx, dy, dx + drawWidth, dy + drawHeight);

        // Draw the resized/padded image with high quality sampling
        using (var paint = new SKPaint { IsAntialias = true })
        {
            canvas.DrawBitmap(srcBitmap, destRect, paint);
        }

        // 2. Draw Subtitles if there's any text
        if (!string.IsNullOrWhiteSpace(text))
        {
            DrawSubtitles(canvas, text, targetWidth, targetHeight);
        }

        // 3. Save to output path
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    private static void DrawSubtitles(SKCanvas canvas, string text, int targetWidth, int targetHeight)
    {
        // Define painting styles using the modern SKFont API
        float fontSize = 50f;
        using var font = new SKFont(_typeface, fontSize);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
        };

        using var strokePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6f,
            StrokeJoin = SKStrokeJoin.Round
        };

        // Text wrapping logic
        var wrappedLines = WrapText(font, text, targetWidth * 0.9f); // 90% of screen width

        // Calculate bottom margin layout
        float lineHeight = fontSize * 1.2f;
        float marginV = 80f; // Margin from the bottom
        float startY = targetHeight - marginV - (wrappedLines.Count - 1) * lineHeight;

        float centerX = targetWidth / 2f;

        for (int i = 0; i < wrappedLines.Count; i++)
        {
            float y = startY + i * lineHeight;

            // Draw outline first
            canvas.DrawText(wrappedLines[i], centerX, y, SKTextAlign.Center, font, strokePaint);
            
            // Draw inner text
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
            {
                currentLine = word;
            }
            else
            {
                string testLine = currentLine + " " + word;
                float width = font.MeasureText(testLine);
                
                if (width > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
        }
        
        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }
        
        return lines;
    }
}
