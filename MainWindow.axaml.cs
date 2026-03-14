using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AutomationContent.ViewModels;

namespace AutomationContent;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Set up drag-and-drop
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    // ═══════════════════════════════════════
    //  DOWNLOAD HANDLERS
    // ═══════════════════════════════════════

    private async void OnGetInfoClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.GetVideoInfoAsync();
    }

    private async void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.DownloadAsync();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.CancelDownload();
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.OpenFolder();
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = StorageProvider;
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Download Folder",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var folder = result[0];
            var path = folder.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                _viewModel.SaveFolder = path;
            }
        }
    }

    // ═══════════════════════════════════════
    //  TRANSCRIPTION HANDLERS
    // ═══════════════════════════════════════

    private async void OnTranscribeDownloadedClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.TranscribeDownloadedFileAsync();
    }

    private async void OnBrowseTranscribeFileClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = StorageProvider;
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a video or audio file to transcribe",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Media Files")
                {
                    Patterns = new[] { "*.mp4", "*.mp3", "*.wav", "*.m4a", "*.webm", "*.ogg", "*.flac" }
                },
                FilePickerFileTypes.All
            }
        });

        if (result.Count > 0)
        {
            var filePath = result[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(filePath))
            {
                await _viewModel.TranscribeFileAsync(filePath);
            }
        }
    }

    private void OnCancelTranscribeClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.CancelTranscription();
    }

    private async void OnCopyTranscriptClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            var text = _viewModel.GetTranscriptForCopy();
            await clipboard.SetTextAsync(text);
            _viewModel.StatusMessage = "📋 Transcript copied to clipboard!";
        }
    }

    private async void OnSaveTranscriptClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.SaveTranscriptAsync();
    }

    // ═══════════════════════════════════════
    //  VOICEOVER HANDLERS
    // ═══════════════════════════════════════

    private void OnPrepareVoiceoverClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.PrepareVoiceoverFromTranscript();
    }

    private async void OnGenerateVoiceoverClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.GenerateVoiceoverAsync();
    }

    private async void OnTranslateClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.TranslateVoiceoverTextAsync();
    }

    private void OnCancelVoiceoverClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.CancelVoiceover();
    }

    private void OnPlayVoiceoverClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.PlayVoiceover();
    }

    private void OnStopVoiceoverClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.StopVoiceover();
    }

    private void OnOpenVoiceoverFolderClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.OpenVoiceoverFolder();
    }

    // ═══════════════════════════════════════
    //  IMAGE GENERATION HANDLERS
    // ═══════════════════════════════════════

    private void OnPrepareImageGenClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.PrepareImageGeneration();
    }

    private async void OnGenerateAllImagesClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.GenerateAllImagesAsync();
    }

    private void OnCancelImageGenClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.CancelImageGeneration();
    }

    private async void OnRetryImageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button button && button.DataContext is AutomationContent.Models.ImageGenerationItem item)
        {
            await _viewModel.RegenerateImageAsync(item);
        }
    }

    private async void OnRegenerateImageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button button && button.DataContext is AutomationContent.Models.ImageGenerationItem item)
        {
            await _viewModel.RegenerateImageAsync(item);
        }
    }

    // ═══════════════════════════════════════
    //  VIDEO RENDER HANDLERS
    // ═══════════════════════════════════════

    private void OnGoToRenderClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.GoToRenderSection();
    }

    private async void OnStartRenderClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.RenderVideoAsync();
    }

    private void OnCancelRenderClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.CancelRender();
    }

    private void OnOpenRenderedVideoFolderClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.OpenRenderedVideoFolder();
    }

    private void OnPreviewRenderedVideoClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.PreviewRenderedVideo();
    }

    // ═══════════════════════════════════════
    //  DRAG AND DROP
    // ═══════════════════════════════════════

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Only accept file drops
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles();
        if (files == null) return;

        var firstFile = files.FirstOrDefault();
        if (firstFile == null) return;

        var filePath = firstFile.TryGetLocalPath();
        if (string.IsNullOrEmpty(filePath)) return;

        if (Services.WhisperService.IsSupportedMediaFile(filePath))
        {
            await _viewModel.TranscribeDroppedFileAsync(filePath);
        }
        else
        {
            _viewModel.ErrorMessage = "Unsupported file format. Please drop .mp4, .mp3, .wav, .m4a, .webm, .ogg, or .flac files.";
        }
    }
}
