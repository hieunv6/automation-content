using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationContent.Services;

/// <summary>
/// Service to interact with Pollinations.AI.
/// - Text API: POST https://text.pollinations.ai/ → summarize paragraph into image prompt
///   (or OpenAI-compatible POST https://gen.pollinations.ai/v1/chat/completions with API key)
/// - Image API: GET https://image.pollinations.ai/prompt/[encoded_prompt]?... → download image
///   (with API key: adds ?key=API_KEY query param)
///
/// API Key is optional — works without it but may be rate limited.
/// Key types: sk_ (secret, server-side) | pk_ (publishable, client-side, rate limited)
/// Docs: https://enter.pollinations.ai/api/docs
/// </summary>
public class PollinationsService
{
    private readonly HttpClient _httpClient;
    private string _apiKey = string.Empty;

    public event Action<string>? StatusChanged;

    /// <summary>
    /// Gets or sets the Pollinations API key.
    /// Empty string = no API key (free mode with rate limits).
    /// Key types: sk_ (secret) or pk_ (publishable).
    /// </summary>
    public string ApiKey
    {
        get => _apiKey;
        set => _apiKey = value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Whether an API key is configured.
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

    public PollinationsService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Load API key from environment variable if present
        var envKey = Environment.GetEnvironmentVariable("POLLINATIONS_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            ApiKey = envKey;
        }
    }

    /// <summary>
    /// Use Pollinations text API to generate a short English image prompt from a paragraph.
    /// If API key is set: uses OpenAI-compatible endpoint with auth header.
    /// If no API key: uses free text.pollinations.ai endpoint.
    /// </summary>
    public async Task<string> GenerateImagePromptAsync(string paragraphText, CancellationToken ct = default)
    {
        try
        {
            string result;

            if (HasApiKey)
            {
                // Use OpenAI-compatible endpoint with API key
                result = await GeneratePromptWithApiKeyAsync(paragraphText, ct);
            }
            else
            {
                // Use free text API (no key required)
                result = await GeneratePromptFreeAsync(paragraphText, ct);
            }

            // Clean up the response — remove quotes, trim
            result = result.Trim().Trim('"').Trim();

            // If response is too long, truncate
            if (result.Length > 200)
            {
                result = result[..200];
            }

            return string.IsNullOrWhiteSpace(result)
                ? "Abstract visual scene"
                : result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusChanged?.Invoke($"⚠️ Prompt generation failed: {ex.Message}");
            // Fallback: use first 20 words of paragraph
            var words = paragraphText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var fallback = string.Join(" ", words.Length > 20 ? words[..20] : words);
            return string.IsNullOrWhiteSpace(fallback) ? "Abstract visual scene" : fallback;
        }
    }

    /// <summary>
    /// Generate prompt using free text API (no key).
    /// </summary>
    private async Task<string> GeneratePromptFreeAsync(string paragraphText, CancellationToken ct)
    {
        var requestBody = $"Summarize this paragraph into a short English image generation prompt (max 20 words), focus on visual elements only. Return ONLY the prompt text, no quotes, no explanation. Paragraph: {paragraphText}";

        var content = new StringContent(requestBody, Encoding.UTF8, "text/plain");

        using var response = await _httpClient.PostAsync("https://text.pollinations.ai/", content, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Generate prompt using OpenAI-compatible endpoint with API key.
    /// POST https://gen.pollinations.ai/v1/chat/completions
    /// </summary>
    private async Task<string> GeneratePromptWithApiKeyAsync(string paragraphText, CancellationToken ct)
    {
        var systemPrompt = "You are a concise image prompt generator. Given a paragraph, output ONLY a short English image generation prompt (max 20 words). Focus on visual elements only. No quotes, no explanation.";
        var userMessage = $"Paragraph: {paragraphText}";

        // Build OpenAI-compatible request body
        var jsonBody = $@"{{
            ""model"": ""openai"",
            ""messages"": [
                {{""role"": ""system"", ""content"": {EscapeJsonString(systemPrompt)}}},
                {{""role"": ""user"", ""content"": {EscapeJsonString(userMessage)}}}
            ],
            ""temperature"": 0.7
        }}";

        var request = new HttpRequestMessage(HttpMethod.Post, "https://gen.pollinations.ai/v1/chat/completions")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync(ct);

        // Parse OpenAI-compatible response to extract content
        // Response format: {"choices":[{"message":{"content":"..."}}]}
        var contentStart = responseText.IndexOf("\"content\":", StringComparison.Ordinal);
        if (contentStart >= 0)
        {
            contentStart = responseText.IndexOf('"', contentStart + 10) + 1;
            var contentEnd = responseText.IndexOf('"', contentStart);
            if (contentEnd > contentStart)
            {
                return responseText[contentStart..contentEnd];
            }
        }

        // Fallback: return raw response trimmed
        return responseText;
    }

    /// <summary>
    /// Generate an image using Pollinations image API and save it to disk.
    /// If API key is set, appends ?key=API_KEY to the request URL.
    /// </summary>
    /// <param name="prompt">The image generation prompt</param>
    /// <param name="styleSuffix">Style suffix to append (e.g. ", professional photography, 4K")</param>
    /// <param name="outputPath">Full path to save the image file (e.g. .../image_001.jpg)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> GenerateImageAsync(string prompt, string styleSuffix, string outputPath, CancellationToken ct = default)
    {
        try
        {
            var fullPrompt = prompt + styleSuffix;
            var encodedPrompt = Uri.EscapeDataString(fullPrompt);

            // Build URL with optional API key
            // var url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=1280&height=720&nologo=true&model=flux";
            var url = $"https://gen.pollinations.ai/image/{encodedPrompt}?model=grok-imagine&width=1024&height=1024&seed=0&enhance=false";
            if (HasApiKey)
            {
                url += $"&key={Uri.EscapeDataString(_apiKey)}";
            }

            // Use a separate client with longer timeout for image downloads
            using var imageClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            // If using API key, also add Authorization header
            if (HasApiKey)
            {
                imageClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            var imageBytes = await imageClient.GetByteArrayAsync(url, ct);

            if (imageBytes.Length < 1000) // too small, likely an error response
            {
                StatusChanged?.Invoke("⚠️ Received invalid image data");
                return false;
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllBytesAsync(outputPath, imageBytes, ct);
            return true;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout — not a user cancellation
            StatusChanged?.Invoke("⚠️ Image generation timed out (>60s). Try again.");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw; // user cancelled
        }
        catch (HttpRequestException ex)
        {
            var msg = ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "⚠️ API key không hợp lệ. Vui lòng kiểm tra lại.",
                HttpStatusCode.PaymentRequired => "⚠️ Tài khoản hết credit. Vui lòng nạp thêm.",
                HttpStatusCode.Forbidden => "⚠️ Không có quyền truy cập. Kiểm tra API key.",
                _ => $"⚠️ Image generation failed: {ex.Message}"
            };
            StatusChanged?.Invoke(msg);
            return false;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"⚠️ Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Escape a string for use in JSON.
    /// </summary>
    private static string EscapeJsonString(string s)
    {
        return "\"" + s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t") + "\"";
    }
}
