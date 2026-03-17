using System.Collections.Generic;

namespace AutomationContent.Models;

public class AppState
{
    // Metadata
    public string ProjectName { get; set; } = string.Empty;
    public string LastModified { get; set; } = string.Empty;

    // Video Info
    public string VideoUrl { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string VideoDuration { get; set; } = string.Empty;
    public string VideoChannel { get; set; } = string.Empty;
    public string VideoViews { get; set; } = string.Empty;
    public bool HasVideoInfo { get; set; }

    // Download
    public string DownloadedFilePath { get; set; } = string.Empty;
    public string SaveFolder { get; set; } = string.Empty;
    public int SelectedQualityIndex { get; set; }

    // Transcription
    public string TranscriptText { get; set; } = string.Empty;
    public string TranscriptLanguage { get; set; } = string.Empty;
    public string TranscribeFilePath { get; set; } = string.Empty;

    // Voiceover
    public string VoiceoverText { get; set; } = string.Empty;
    public string VoiceoverFilePath { get; set; } = string.Empty;
    public List<double> VoiceoverChunkDurations { get; set; } = new();
    public int SelectedLanguageIndex { get; set; }
    public int SelectedVoiceIndex { get; set; }
    public int SelectedSpeedIndex { get; set; } = 1;

    // Images
    public List<ImageState> ImageItems { get; set; } = new();
    public int SelectedImageStyleIndex { get; set; }
    public bool ShowImageSection { get; set; }
    public bool AllImagesReady { get; set; }

    // Render
    public bool ShowRenderSection { get; set; }
    public string RenderedVideoPath { get; set; } = string.Empty;
    public int SelectedResolutionIndex { get; set; }
    public int SelectedSubtitleModeIndex { get; set; }

    // Wizard step
    public int CurrentStep { get; set; }
}

public class ImageState
{
    public int Index { get; set; }
    public string ParagraphText { get; set; } = string.Empty;
    public string ImagePrompt { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}
