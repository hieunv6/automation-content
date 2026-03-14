using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace AutomationContent.Services;

/// <summary>
/// Free translation service using Google Translate (unofficial endpoint — no API key needed).
/// </summary>
public class TranslationService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Translate text from source language to target language using Google Translate.
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="sourceLang">Source language code (e.g., "vi", "en", "auto")</param>
    /// <param name="targetLang">Target language code (e.g., "en", "ja", "ko")</param>
    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (sourceLang == targetLang)
            return text;

        // Split long text into chunks (Google Translate has a limit of ~5000 chars)
        var chunks = SplitForTranslation(text, 4500);
        var translatedChunks = new List<string>();

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            var translated = await TranslateChunkAsync(chunk, sourceLang, targetLang, ct);
            translatedChunks.Add(translated);
        }

        return string.Join("\n\n", translatedChunks);
    }

    private async Task<string> TranslateChunkAsync(string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        try
        {
            var encodedText = HttpUtility.UrlEncode(text);
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={encodedText}";

            var response = await _httpClient.GetStringAsync(url, ct);

            // Parse the JSON response: [[["translated text","original text",null,null,10]],null,"vi",...]
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var result = new List<string>();
            if (root.GetArrayLength() > 0)
            {
                var translations = root[0];
                foreach (var segment in translations.EnumerateArray())
                {
                    if (segment.GetArrayLength() > 0)
                    {
                        var translatedText = segment[0].GetString();
                        if (!string.IsNullOrEmpty(translatedText))
                            result.Add(translatedText);
                    }
                }
            }

            return string.Join("", result);
        }
        catch (HttpRequestException)
        {
            throw new Exception("Không thể kết nối đến dịch vụ dịch thuật. Vui lòng kiểm tra kết nối mạng.");
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception($"Lỗi dịch thuật: {ex.Message}");
        }
    }

    private static List<string> SplitForTranslation(string text, int maxChars)
    {
        var chunks = new List<string>();
        if (text.Length <= maxChars)
        {
            chunks.Add(text);
            return chunks;
        }

        // Split by double newline (paragraphs)
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.None);
        var currentChunk = "";

        foreach (var para in paragraphs)
        {
            if ((currentChunk + "\n\n" + para).Length > maxChars && !string.IsNullOrEmpty(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = para;
            }
            else
            {
                currentChunk = string.IsNullOrEmpty(currentChunk) ? para : currentChunk + "\n\n" + para;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
            chunks.Add(currentChunk.Trim());

        return chunks;
    }
}
