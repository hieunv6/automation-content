using System.Collections.Generic;

namespace AutomationContent.Models;

/// <summary>
/// Represents a voice available in Edge TTS.
/// </summary>
public class TtsVoice
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty; // "Nữ" or "Nam"
}

/// <summary>
/// Represents a supported language with its translation code and available Edge TTS voices.
/// </summary>
public class SupportedLanguage
{
    public string DisplayName { get; init; } = string.Empty;
    public string TranslateCode { get; init; } = string.Empty;  // Google Translate code
    public string Flag { get; init; } = string.Empty;
    public List<TtsVoice> Voices { get; init; } = new();

    public override string ToString() => $"{Flag} {DisplayName}";
}

/// <summary>
/// Registry of all supported languages and their Edge TTS voices.
/// </summary>
public static class LanguageRegistry
{
    public static readonly List<SupportedLanguage> Languages = new()
    {
        new SupportedLanguage
        {
            DisplayName = "Tiếng Việt",
            TranslateCode = "vi",
            Flag = "🇻🇳",
            Voices = new()
            {
                new TtsVoice { Id = "vi-VN-HoaiMyNeural", DisplayName = "HoaiMy — Nữ miền Nam", Gender = "Nữ" },
                new TtsVoice { Id = "vi-VN-NamMinhNeural", DisplayName = "NamMinh — Nam miền Nam", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "English (US)",
            TranslateCode = "en",
            Flag = "🇺🇸",
            Voices = new()
            {
                new TtsVoice { Id = "en-US-JennyNeural", DisplayName = "Jenny — Female", Gender = "Nữ" },
                new TtsVoice { Id = "en-US-GuyNeural", DisplayName = "Guy — Male", Gender = "Nam" },
                new TtsVoice { Id = "en-US-AriaNeural", DisplayName = "Aria — Female (news)", Gender = "Nữ" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "English (UK)",
            TranslateCode = "en",
            Flag = "🇬🇧",
            Voices = new()
            {
                new TtsVoice { Id = "en-GB-SoniaNeural", DisplayName = "Sonia — Female", Gender = "Nữ" },
                new TtsVoice { Id = "en-GB-RyanNeural", DisplayName = "Ryan — Male", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "日本語 (Japanese)",
            TranslateCode = "ja",
            Flag = "🇯🇵",
            Voices = new()
            {
                new TtsVoice { Id = "ja-JP-NanamiNeural", DisplayName = "Nanami — 女性", Gender = "Nữ" },
                new TtsVoice { Id = "ja-JP-KeitaNeural", DisplayName = "Keita — 男性", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "한국어 (Korean)",
            TranslateCode = "ko",
            Flag = "🇰🇷",
            Voices = new()
            {
                new TtsVoice { Id = "ko-KR-SunHiNeural", DisplayName = "SunHi — 여성", Gender = "Nữ" },
                new TtsVoice { Id = "ko-KR-InJoonNeural", DisplayName = "InJoon — 남성", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "中文 (Chinese)",
            TranslateCode = "zh-CN",
            Flag = "🇨🇳",
            Voices = new()
            {
                new TtsVoice { Id = "zh-CN-XiaoxiaoNeural", DisplayName = "Xiaoxiao — 女", Gender = "Nữ" },
                new TtsVoice { Id = "zh-CN-YunxiNeural", DisplayName = "Yunxi — 男", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "ภาษาไทย (Thai)",
            TranslateCode = "th",
            Flag = "🇹🇭",
            Voices = new()
            {
                new TtsVoice { Id = "th-TH-PremwadeeNeural", DisplayName = "Premwadee — หญิง", Gender = "Nữ" },
                new TtsVoice { Id = "th-TH-NiwatNeural", DisplayName = "Niwat — ชาย", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "Français (French)",
            TranslateCode = "fr",
            Flag = "🇫🇷",
            Voices = new()
            {
                new TtsVoice { Id = "fr-FR-DeniseNeural", DisplayName = "Denise — Femme", Gender = "Nữ" },
                new TtsVoice { Id = "fr-FR-HenriNeural", DisplayName = "Henri — Homme", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "Español (Spanish)",
            TranslateCode = "es",
            Flag = "🇪🇸",
            Voices = new()
            {
                new TtsVoice { Id = "es-ES-ElviraNeural", DisplayName = "Elvira — Mujer", Gender = "Nữ" },
                new TtsVoice { Id = "es-ES-AlvaroNeural", DisplayName = "Alvaro — Hombre", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "Deutsch (German)",
            TranslateCode = "de",
            Flag = "🇩🇪",
            Voices = new()
            {
                new TtsVoice { Id = "de-DE-KatjaNeural", DisplayName = "Katja — Weiblich", Gender = "Nữ" },
                new TtsVoice { Id = "de-DE-ConradNeural", DisplayName = "Conrad — Männlich", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "Português (Brazilian)",
            TranslateCode = "pt",
            Flag = "🇧🇷",
            Voices = new()
            {
                new TtsVoice { Id = "pt-BR-FranciscaNeural", DisplayName = "Francisca — Feminino", Gender = "Nữ" },
                new TtsVoice { Id = "pt-BR-AntonioNeural", DisplayName = "Antonio — Masculino", Gender = "Nam" },
            }
        },
        new SupportedLanguage
        {
            DisplayName = "हिन्दी (Hindi)",
            TranslateCode = "hi",
            Flag = "🇮🇳",
            Voices = new()
            {
                new TtsVoice { Id = "hi-IN-SwaraNeural", DisplayName = "Swara — महिला", Gender = "Nữ" },
                new TtsVoice { Id = "hi-IN-MadhurNeural", DisplayName = "Madhur — पुरुष", Gender = "Nam" },
            }
        },
    };
}
