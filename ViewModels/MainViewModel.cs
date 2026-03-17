using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationContent.Models;
using AutomationContent.Services;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Newtonsoft.Json;

namespace AutomationContent.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly YtDlpService _ytDlpService;
    private readonly WhisperService _whisperService;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _transcribeCts;

    // ═══════════════════════════════════════
    //  STEP-BASED WIZARD NAVIGATION
    // ═══════════════════════════════════════

    public static readonly string[] StepNames = { "Download", "Transcribe", "Voiceover", "Images", "Render" };
    public static readonly string[] StepIcons = { "⬇️", "🎙️", "🎤", "🖼️", "🎬" };

    private int _currentStep;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (value < 0 || value >= StepNames.Length) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStep0));
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(IsStep4));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CurrentStepName));
            if (_currentStep == 4) InitializePreview();
            AutoSave();
        }
    }

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;

    public bool CanGoBack => CurrentStep > 0;
    public bool CanGoNext => CurrentStep < StepNames.Length - 1;
    public string CurrentStepName => StepNames[CurrentStep];

    public void GoToStep(int step)
    {
        CurrentStep = step;
    }

    public void NextStep()
    {
        if (CanGoNext) CurrentStep++;
    }

    public void PrevStep()
    {
        if (CanGoBack) CurrentStep--;
    }

    // ═══════════════════════════════════════
    //  PROJECT DELETE CONFIRMATION
    // ═══════════════════════════════════════

    private bool _showDeleteConfirm;
    public bool ShowDeleteConfirm
    {
        get => _showDeleteConfirm;
        set { _showDeleteConfirm = value; OnPropertyChanged(); }
    }

    private string _deleteTargetProjectName = string.Empty;
    public string DeleteTargetProjectName
    {
        get => _deleteTargetProjectName;
        set { _deleteTargetProjectName = value; OnPropertyChanged(); }
    }

    public void RequestDeleteProject(string name)
    {
        DeleteTargetProjectName = name;
        ShowDeleteConfirm = true;
    }

    public void ConfirmDeleteProject()
    {
        if (!string.IsNullOrEmpty(DeleteTargetProjectName))
        {
            DeleteProject(DeleteTargetProjectName);
        }
        ShowDeleteConfirm = false;
        DeleteTargetProjectName = string.Empty;
    }

    public void CancelDeleteProject()
    {
        ShowDeleteConfirm = false;
        DeleteTargetProjectName = string.Empty;
    }

    public MainViewModel()
    {
        _ytDlpService = new YtDlpService();
        _whisperService = new WhisperService();

        _ytDlpService.ProgressChanged += (percent, status) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                DownloadProgress = percent;
                ProgressText = status;
            });
        };

        _ytDlpService.StatusChanged += (status) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = status;
            });
        };

        _whisperService.StatusChanged += (status) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = status;
            });
        };

        // Set default save folder
        SaveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        // Check yt-dlp
        if (!_ytDlpService.IsYtDlpInstalled())
        {
            StatusMessage = "⚠️ yt-dlp is not installed. Please install it first (brew install yt-dlp).";
            IsYtDlpMissing = true;
        }

        SelectedQualityIndex = 1; // Default: 720p

        // Ensure projects folder exists
        if (!Directory.Exists(_projectsBaseDir))
            Directory.CreateDirectory(_projectsBaseDir);

        // Load saved state
        LoadState();
        RefreshProjectsList();
    }

    private bool _isLoadingState;
    private string _currentProjectName = "Default Project";
    public string CurrentProjectName
    {
        get => _currentProjectName;
        set 
        { 
            if (string.IsNullOrWhiteSpace(value) || value == _currentProjectName) return;

            var newName = string.Join("_", value.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrEmpty(newName) || newName == _currentProjectName) return;

            var oldPath = Path.Combine(_projectsBaseDir, $"{_currentProjectName}.json");
            var newPath = Path.Combine(_projectsBaseDir, $"{newName}.json");

            if (File.Exists(newPath) && newName != _currentProjectName)
            {
                ErrorMessage = "A project with this name already exists.";
                OnPropertyChanged(); // Revert back in UI
                return;
            }

            if (!string.IsNullOrEmpty(_currentProjectName) && File.Exists(oldPath))
            {
                try { File.Move(oldPath, newPath); } catch { }
            }

            _currentProjectName = newName; 
            OnPropertyChanged(); 
            AutoSave(); 
            RefreshProjectsList();
        }
    }

    private ObservableCollection<ProjectInfo> _projectsList = new();
    public ObservableCollection<ProjectInfo> ProjectsList
    {
        get => _projectsList;
        set { _projectsList = value; OnPropertyChanged(); }
    }

    private readonly string _projectsBaseDir = Path.Combine(AppContext.BaseDirectory, "Projects");
    private string StateFilePath => Path.Combine(_projectsBaseDir, $"{CurrentProjectName}.json");

    private void RefreshProjectsList()
    {
        try
        {
            if (!Directory.Exists(_projectsBaseDir))
                Directory.CreateDirectory(_projectsBaseDir);

            var files = Directory.GetFiles(_projectsBaseDir, "*.json");
            var list = new List<ProjectInfo>();
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var state = JsonConvert.DeserializeObject<AppState>(json);
                    if (state != null)
                    {
                        list.Add(new ProjectInfo { 
                            Name = Path.GetFileNameWithoutExtension(file),
                            LastModified = state.LastModified,
                            VideoTitle = state.VideoTitle
                        });
                    }
                }
                catch { }
            }
            ProjectsList = new ObservableCollection<ProjectInfo>(list.OrderByDescending(p => p.LastModified));
        }
        catch { }
    }

    public void CreateNewProject()
    {
        // For simplicity, generate a name or ask? 
        // Let's create a "New Project [Timestamp]"
        var name = $"Project {DateTime.Now:yyyyMMdd_HHmmss}";
        CurrentProjectName = name;
        
        // Reset all fields
        ResetAllFields();
        
        AutoSave();
        RefreshProjectsList();
    }

    private void ResetAllFields()
    {
        _isLoadingState = true;
        try
        {
            VideoUrl = "";
            VideoTitle = "";
            VideoDuration = "";
            VideoChannel = "";
            VideoViews = "";
            HasVideoInfo = false;
            DownloadedFilePath = "";
            TranscriptText = "";
            TranscriptLanguage = "";
            TranscribeFilePath = "";
            VoiceoverText = "";
            VoiceoverFilePath = "";
            VoiceoverChunkDurations = new();
            ImageItems.Clear();
            ShowImageSection = false;
            AllImagesReady = false;
            ShowRenderSection = false;
            RenderedVideoPath = "";
            SuccessMessage = "";
            ErrorMessage = "";
            StatusMessage = "New project created.";
            ThumbnailBitmap = null;
        }
        finally
        {
            _isLoadingState = false;
        }
    }

    public void LoadProjectByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        _currentProjectName = name; // set backing field to avoid renaming side-effects
        OnPropertyChanged(nameof(CurrentProjectName));
        LoadState();
    }

    public void DeleteProject(string name)
    {
        try
        {
            var path = Path.Combine(_projectsBaseDir, $"{name}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
                RefreshProjectsList();
                if (CurrentProjectName == name)
                {
                    CreateNewProject();
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not delete project: {ex.Message}";
        }
    }

    private void AutoSave()
    {
        if (_isLoadingState) return;
        
        try
        {
            if (!Directory.Exists(_projectsBaseDir))
                Directory.CreateDirectory(_projectsBaseDir);

            var state = new AppState
            {
                ProjectName = CurrentProjectName,
                LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                VideoUrl = VideoUrl,
                VideoTitle = VideoTitle,
                VideoDuration = VideoDuration,
                VideoChannel = VideoChannel,
                VideoViews = VideoViews,
                HasVideoInfo = HasVideoInfo,
                DownloadedFilePath = DownloadedFilePath,
                SaveFolder = SaveFolder,
                SelectedQualityIndex = SelectedQualityIndex,
                TranscriptText = TranscriptText,
                TranscriptLanguage = TranscriptLanguage,
                TranscribeFilePath = TranscribeFilePath,
                VoiceoverText = VoiceoverText,
                VoiceoverFilePath = VoiceoverFilePath,
                VoiceoverChunkDurations = VoiceoverChunkDurations,
                SelectedLanguageIndex = SelectedLanguageIndex,
                SelectedVoiceIndex = SelectedVoiceIndex,
                SelectedSpeedIndex = SelectedSpeedIndex,
                SelectedImageStyleIndex = SelectedImageStyleIndex,
                ShowImageSection = ShowImageSection,
                AllImagesReady = AllImagesReady,
                ShowRenderSection = ShowRenderSection,
                RenderedVideoPath = RenderedVideoPath,
                SelectedResolutionIndex = SelectedResolutionIndex,
                SelectedSubtitleModeIndex = SelectedSubtitleModeIndex,
                CurrentStep = CurrentStep,
                ImageItems = ImageItems.Select(i => new ImageState
                {
                    Index = i.Index,
                    ParagraphText = i.ParagraphText,
                    ImagePrompt = i.ImagePrompt,
                    ImagePath = i.ImagePath
                }).ToList()
            };

            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(StateFilePath, json);
        }
        catch { /* Ignore save errors in background */ }
    }

    private void LoadState()
    {
        // Try the last used project if name is not set, or use project file
        var path = StateFilePath;
        if (!File.Exists(path))
        {
            // Try to find any project to load
            RefreshProjectsList();
            if (ProjectsList.Count > 0)
            {
                _currentProjectName = ProjectsList[0].Name;
                path = StateFilePath;
            }
            else
            {
                return;
            }
        }

        try
        {
            _isLoadingState = true;
            var json = File.ReadAllText(path);
            var state = JsonConvert.DeserializeObject<AppState>(json);
            if (state == null) return;

            // Apply state
            _currentProjectName = state.ProjectName;
            OnPropertyChanged(nameof(CurrentProjectName));
            
            VideoUrl = state.VideoUrl;
            VideoTitle = state.VideoTitle;
            VideoDuration = state.VideoDuration;
            VideoChannel = state.VideoChannel;
            VideoViews = state.VideoViews;
            HasVideoInfo = state.HasVideoInfo;
            DownloadedFilePath = state.DownloadedFilePath;
            if (!string.IsNullOrEmpty(state.SaveFolder)) SaveFolder = state.SaveFolder;
            SelectedQualityIndex = state.SelectedQualityIndex;
            TranscriptText = state.TranscriptText;
            TranscriptLanguage = state.TranscriptLanguage;
            TranscribeFilePath = state.TranscribeFilePath;
            VoiceoverText = state.VoiceoverText;
            VoiceoverFilePath = state.VoiceoverFilePath;
            VoiceoverChunkDurations = state.VoiceoverChunkDurations ?? new();
            SelectedLanguageIndex = state.SelectedLanguageIndex;
            SelectedVoiceIndex = state.SelectedVoiceIndex;
            SelectedSpeedIndex = state.SelectedSpeedIndex;
            SelectedImageStyleIndex = state.SelectedImageStyleIndex;
            ShowImageSection = state.ShowImageSection;
            AllImagesReady = state.AllImagesReady;
            ShowRenderSection = state.ShowRenderSection;
            RenderedVideoPath = state.RenderedVideoPath;
            SelectedResolutionIndex = state.SelectedResolutionIndex;
            SelectedSubtitleModeIndex = state.SelectedSubtitleModeIndex;
            CurrentStep = state.CurrentStep;

            // Load images
            ImageItems.Clear();
            if (state.ImageItems != null)
            {
                foreach (var img in state.ImageItems)
                {
                    var item = new ImageGenerationItem
                    {
                        Index = img.Index,
                        ParagraphText = img.ParagraphText,
                        ImagePrompt = img.ImagePrompt,
                        ImagePath = img.ImagePath
                    };
                    
                    item.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(ImageGenerationItem.ImagePrompt))
                            AutoSave();
                    };

                    // Lazy load bitmap
                    if (!string.IsNullOrEmpty(img.ImagePath) && File.Exists(img.ImagePath))
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                var bytes = await File.ReadAllBytesAsync(img.ImagePath);
                                Dispatcher.UIThread.Post(() =>
                                {
                                    using var ms = new MemoryStream(bytes);
                                    item.ImageBitmap = new Bitmap(ms);
                                });
                            }
                            catch { }
                        });
                    }
                    ImageItems.Add(item);
                }
            }

            // If we have rendered video, refresh info
            if (HasRenderedVideo)
            {
                Task.Run(async () => await RefreshRenderedVideoInfoAsync());
            }

            StatusMessage = $"Project '{CurrentProjectName}' restored.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load state: {ex.Message}");
        }
        finally
        {
            _isLoadingState = false;
        }
    }

    private async Task RefreshRenderedVideoInfoAsync()
    {
        try
        {
            var (duration, size) = await _videoRenderService.GetVideoInfoAsync(RenderedVideoPath);
            var sizeText = VideoRenderService.FormatFileSize(size);
            var durationText = TimeSpan.FromSeconds(duration).ToString(@"mm\:ss");
            Dispatcher.UIThread.Post(() => {
                RenderedVideoInfo = $"Độ dài: {durationText} | Dung lượng: {sizeText}";
            });
        }
        catch { }
    }

    // Properties
    private string _videoUrl = string.Empty;
    public string VideoUrl
    {
        get => _videoUrl;
        set
        {
            _videoUrl = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGetInfo));
            // Clear previous state when URL changes
            if (HasVideoInfo)
            {
                HasVideoInfo = false;
                VideoTitle = string.Empty;
                VideoDuration = string.Empty;
                VideoChannel = string.Empty;
                VideoViews = string.Empty;
                ThumbnailBitmap = null;
            }
            ClearMessages();
            AutoSave();
        }
    }

    private string _videoTitle = string.Empty;
    public string VideoTitle
    {
        get => _videoTitle;
        set { _videoTitle = value; OnPropertyChanged(); }
    }

    private string _videoDuration = string.Empty;
    public string VideoDuration
    {
        get => _videoDuration;
        set { _videoDuration = value; OnPropertyChanged(); }
    }

    private string _videoChannel = string.Empty;
    public string VideoChannel
    {
        get => _videoChannel;
        set { _videoChannel = value; OnPropertyChanged(); }
    }

    private string _videoViews = string.Empty;
    public string VideoViews
    {
        get => _videoViews;
        set { _videoViews = value; OnPropertyChanged(); }
    }

    private Bitmap? _thumbnailBitmap;
    public Bitmap? ThumbnailBitmap
    {
        get => _thumbnailBitmap;
        set { _thumbnailBitmap = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasThumbnail)); }
    }

    public bool HasThumbnail => ThumbnailBitmap != null;

    private bool _hasVideoInfo;
    public bool HasVideoInfo
    {
        get => _hasVideoInfo;
        set
        {
            _hasVideoInfo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanDownload));
        }
    }

    private bool _isLoadingInfo;
    public bool IsLoadingInfo
    {
        get => _isLoadingInfo;
        set
        {
            _isLoadingInfo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGetInfo));
            OnPropertyChanged(nameof(CanDownload));
        }
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            _isDownloading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanDownload));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanGetInfo));
        }
    }

    private double _downloadProgress;
    public double DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); }
    }

    private string _progressText = string.Empty;
    public string ProgressText
    {
        get => _progressText;
        set { _progressText = value; OnPropertyChanged(); }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private string _successMessage = string.Empty;
    public string SuccessMessage
    {
        get => _successMessage;
        set { _successMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSuccess)); }
    }

    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    private string _downloadedFilePath = string.Empty;
    public string DownloadedFilePath
    {
        get => _downloadedFilePath;
        set { _downloadedFilePath = value; OnPropertyChanged(); AutoSave(); }
    }

    private string _saveFolder = string.Empty;
    public string SaveFolder
    {
        get => _saveFolder;
        set { _saveFolder = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); AutoSave(); }
    }

    private int _selectedQualityIndex;
    public int SelectedQualityIndex
    {
        get => _selectedQualityIndex;
        set { _selectedQualityIndex = value; OnPropertyChanged(); AutoSave(); }
    }

    private bool _isYtDlpMissing;
    public bool IsYtDlpMissing
    {
        get => _isYtDlpMissing;
        set { _isYtDlpMissing = value; OnPropertyChanged(); }
    }

    public VideoQuality SelectedQuality => SelectedQualityIndex switch
    {
        0 => VideoQuality.Quality1080p,
        1 => VideoQuality.Quality720p,
        2 => VideoQuality.Quality480p,
        3 => VideoQuality.AudioOnly,
        _ => VideoQuality.Quality720p
    };

    // Computed
    public bool CanGetInfo => !string.IsNullOrWhiteSpace(VideoUrl) && !IsLoadingInfo && !IsDownloading;
    public bool CanDownload => HasVideoInfo && !IsDownloading && !IsLoadingInfo && !string.IsNullOrWhiteSpace(SaveFolder);
    public bool CanCancel => IsDownloading;

    // Commands
    public async Task GetVideoInfoAsync()
    {
        ClearMessages();

        if (!YtDlpService.IsValidYouTubeUrl(VideoUrl))
        {
            ErrorMessage = "Please enter a valid YouTube URL.\nExamples: youtube.com/watch?v=... or youtu.be/...";
            return;
        }

        IsLoadingInfo = true;

        try
        {
            var info = await _ytDlpService.GetVideoInfoAsync(VideoUrl);
            if (info != null)
            {
                VideoTitle = info.Title;
                VideoDuration = info.FormattedDuration;
                VideoChannel = info.Channel;
                VideoViews = info.FormattedViewCount;
                HasVideoInfo = true;
                StatusMessage = "Video info loaded successfully!";

                // Load thumbnail
                if (!string.IsNullOrEmpty(info.ThumbnailUrl))
                {
                    await LoadThumbnailAsync(info.ThumbnailUrl);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasVideoInfo = false;
        }
        finally
        {
            IsLoadingInfo = false;
        }
    }

    private async Task LoadThumbnailAsync(string thumbnailUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var imageData = await httpClient.GetByteArrayAsync(thumbnailUrl);
            using var stream = new MemoryStream(imageData);
            ThumbnailBitmap = new Bitmap(stream);
        }
        catch
        {
            // Thumbnail loading failure is non-critical
            ThumbnailBitmap = null;
        }
    }

    public async Task DownloadAsync()
    {
        ClearMessages();
        _downloadCts = new CancellationTokenSource();
        IsDownloading = true;
        DownloadProgress = 0;
        ProgressText = "Starting...";

        try
        {
            var filePath = await _ytDlpService.DownloadAsync(VideoUrl, SaveFolder, SelectedQuality, _downloadCts.Token);
            DownloadedFilePath = filePath;
            DownloadProgress = 100;
            ProgressText = "Complete!";
            SuccessMessage = $"Done! File saved to:\n{filePath}";
            StatusMessage = "Download completed successfully! 🎉";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled.";
            DownloadProgress = 0;
            ProgressText = "Cancelled";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            DownloadProgress = 0;
            ProgressText = "Failed";
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    public void CancelDownload()
    {
        _downloadCts?.Cancel();
        _ytDlpService.CancelDownload();
    }

    public void OpenFolder()
    {
        try
        {
            var path = File.Exists(DownloadedFilePath)
                ? Path.GetDirectoryName(DownloadedFilePath) ?? SaveFolder
                : SaveFolder;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                Process.Start("xdg-open", path);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not open folder: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════
    //  TRANSCRIPTION (Whisper.net — local, no API key needed)
    // ═══════════════════════════════════════════════════

    private bool _isTranscribing;
    public bool IsTranscribing
    {
        get => _isTranscribing;
        set
        {
            _isTranscribing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanTranscribe));
        }
    }

    private string _transcriptText = string.Empty;
    public string TranscriptText
    {
        get => _transcriptText;
        set
        {
            _transcriptText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTranscript));
            OnPropertyChanged(nameof(CanGenerateVoiceover));
            OnPropertyChanged(nameof(CanGenerateImages));
            AutoSave();
        }
    }

    public bool HasTranscript => !string.IsNullOrEmpty(TranscriptText);

    private string _transcriptLanguage = string.Empty;
    public string TranscriptLanguage
    {
        get => _transcriptLanguage;
        set { _transcriptLanguage = value; OnPropertyChanged(); AutoSave(); }
    }

    private string _transcribeFilePath = string.Empty;
    public string TranscribeFilePath
    {
        get => _transcribeFilePath;
        set { _transcribeFilePath = value; OnPropertyChanged(); AutoSave(); }
    }

    public bool CanTranscribe => !IsTranscribing;

    /// <summary>
    /// Transcribe the downloaded video file.
    /// </summary>
    public async Task TranscribeDownloadedFileAsync()
    {
        if (string.IsNullOrEmpty(DownloadedFilePath) || !File.Exists(DownloadedFilePath))
        {
            ErrorMessage = "No downloaded file found to transcribe.";
            return;
        }

        await TranscribeFileAsync(DownloadedFilePath);
    }

    /// <summary>
    /// Transcribe a file using Whisper.net (local, no API key needed).
    /// </summary>
    public async Task TranscribeFileAsync(string filePath)
    {
        if (!WhisperService.IsSupportedMediaFile(filePath))
        {
            ErrorMessage = "Unsupported file format. Please use .mp4, .mp3, .wav, .m4a, .webm, .ogg, or .flac";
            return;
        }

        ClearMessages();
        TranscriptText = string.Empty;
        TranscriptLanguage = string.Empty;
        TranscribeFilePath = filePath;
        IsTranscribing = true;
        _transcribeCts = new CancellationTokenSource();

        string? tempAudioPath = null;

        try
        {
            var audioPath = filePath;

            // Extract/convert audio to 16kHz mono WAV (required by Whisper)
            if (WhisperService.NeedsAudioExtraction(filePath))
            {
                tempAudioPath = await _whisperService.ExtractAudioAsync(filePath, _transcribeCts.Token);
                audioPath = tempAudioPath;
            }

            // Transcribe locally using Whisper.net
            var result = await _whisperService.TranscribeAsync(audioPath, _transcribeCts.Token);

            TranscriptText = result.ToFormattedText();
            TranscriptLanguage = result.DetectedLanguage;
            StatusMessage = $"✅ Transcription complete! Language: {result.DetectedLanguage}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Transcription cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsTranscribing = false;
            _transcribeCts?.Dispose();
            _transcribeCts = null;

            // Clean up temp audio file
            if (tempAudioPath != null && File.Exists(tempAudioPath))
            {
                try { File.Delete(tempAudioPath); } catch { }
            }
        }
    }

    /// <summary>
    /// Handle drag-and-drop file for transcription.
    /// </summary>
    public async Task TranscribeDroppedFileAsync(string filePath)
    {
        await TranscribeFileAsync(filePath);
    }

    public void CancelTranscription()
    {
        _transcribeCts?.Cancel();
    }

    /// <summary>
    /// Copy transcript text to clipboard.
    /// </summary>
    public string GetTranscriptForCopy()
    {
        return TranscriptText;
    }

    /// <summary>
    /// Save transcript as .txt file in the same folder as the video.
    /// </summary>
    public async Task SaveTranscriptAsync()
    {
        if (string.IsNullOrEmpty(TranscriptText)) return;

        try
        {
            var sourceFile = !string.IsNullOrEmpty(TranscribeFilePath) && File.Exists(TranscribeFilePath)
                ? TranscribeFilePath
                : DownloadedFilePath;

            var folder = !string.IsNullOrEmpty(sourceFile) && File.Exists(sourceFile)
                ? Path.GetDirectoryName(sourceFile) ?? SaveFolder
                : SaveFolder;

            var baseName = !string.IsNullOrEmpty(sourceFile) && File.Exists(sourceFile)
                ? Path.GetFileNameWithoutExtension(sourceFile)
                : "transcript";

            var txtPath = Path.Combine(folder, $"{baseName}_transcript.txt");

            // Avoid overwriting
            var counter = 1;
            while (File.Exists(txtPath))
            {
                txtPath = Path.Combine(folder, $"{baseName}_transcript_{counter}.txt");
                counter++;
            }

            await File.WriteAllTextAsync(txtPath, TranscriptText);
            StatusMessage = $"✅ Transcript saved to: {txtPath}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save transcript: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════
    //  AI VOICEOVER (Edge TTS — free, no API key needed)
    //  + Translation (Google Translate — free)
    // ═══════════════════════════════════════════════════

    private readonly EdgeTtsService _edgeTtsService = new();
    private readonly TranslationService _translationService = new();
    private CancellationTokenSource? _voiceoverCts;
    private Process? _audioPlayerProcess;

    // --- Language selection ---

    private int _selectedLanguageIndex;
    public int SelectedLanguageIndex
    {
        get => _selectedLanguageIndex;
        set
        {
            _selectedLanguageIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedLanguage));
            // Reset voice index when language changes
            SelectedVoiceIndex = 0;
            OnPropertyChanged(nameof(VoiceDisplayNames));
            AutoSave();
        }
    }

    public SupportedLanguage SelectedLanguage =>
        (_selectedLanguageIndex >= 0 && _selectedLanguageIndex < LanguageRegistry.Languages.Count)
            ? LanguageRegistry.Languages[_selectedLanguageIndex]
            : LanguageRegistry.Languages[0];

    /// <summary>
    /// Display names for voices of the currently selected language (for ItemsSource binding).
    /// </summary>
    public List<string> VoiceDisplayNames =>
        SelectedLanguage.Voices.ConvertAll(v => $"🎙️ {v.DisplayName}");

    // --- Translation ---

    private bool _isTranslating;
    public bool IsTranslating
    {
        get => _isTranslating;
        set
        {
            _isTranslating = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanTranslate));
            OnPropertyChanged(nameof(CanGenerateVoiceover));
        }
    }

    public bool CanTranslate => !IsTranslating && !IsGeneratingVoiceover && !string.IsNullOrWhiteSpace(VoiceoverText);

    // --- Voiceover text ---

    private string _voiceoverText = string.Empty;
    public string VoiceoverText
    {
        get => _voiceoverText;
        set
        {
            _voiceoverText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGenerateVoiceover));
            OnPropertyChanged(nameof(CanTranslate));
            AutoSave();
        }
    }

    private int _selectedVoiceIndex;
    public int SelectedVoiceIndex
    {
        get => _selectedVoiceIndex;
        set { _selectedVoiceIndex = value; OnPropertyChanged(); AutoSave(); }
    }

    private int _selectedSpeedIndex = 1; // Default: Bình thường
    public int SelectedSpeedIndex
    {
        get => _selectedSpeedIndex;
        set { _selectedSpeedIndex = value; OnPropertyChanged(); AutoSave(); }
    }

    private bool _isGeneratingVoiceover;
    public bool IsGeneratingVoiceover
    {
        get => _isGeneratingVoiceover;
        set
        {
            _isGeneratingVoiceover = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGenerateVoiceover));
            OnPropertyChanged(nameof(CanTranslate));
        }
    }

    private string _voiceoverProgressText = string.Empty;
    public string VoiceoverProgressText
    {
        get => _voiceoverProgressText;
        set { _voiceoverProgressText = value; OnPropertyChanged(); }
    }

    private string _voiceoverFilePath = string.Empty;
    public string VoiceoverFilePath
    {
        get => _voiceoverFilePath;
        set
        {
            _voiceoverFilePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasVoiceover));
            OnPropertyChanged(nameof(CanStartRender));
            AutoSave();
        }
    }

    public List<double> VoiceoverChunkDurations { get; private set; } = new();

    public bool HasVoiceover => !string.IsNullOrEmpty(VoiceoverFilePath) && File.Exists(VoiceoverFilePath);

    private bool _isPlayingAudio;
    public bool IsPlayingAudio
    {
        get => _isPlayingAudio;
        set { _isPlayingAudio = value; OnPropertyChanged(); }
    }

    private string _voiceoverSuccessMessage = string.Empty;
    public string VoiceoverSuccessMessage
    {
        get => _voiceoverSuccessMessage;
        set
        {
            _voiceoverSuccessMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasVoiceoverSuccess));
        }
    }

    public bool HasVoiceoverSuccess => !string.IsNullOrEmpty(VoiceoverSuccessMessage);

    public bool CanGenerateVoiceover => !IsGeneratingVoiceover && !IsTranslating && !string.IsNullOrWhiteSpace(VoiceoverText);

    /// <summary>
    /// Gets the selected voice ID from the current language's voice list.
    /// </summary>
    private string SelectedVoiceName
    {
        get
        {
            var voices = SelectedLanguage.Voices;
            var index = (_selectedVoiceIndex >= 0 && _selectedVoiceIndex < voices.Count)
                ? _selectedVoiceIndex : 0;
            return voices[index].Id;
        }
    }

    /// <summary>
    /// Gets the rate parameter based on dropdown index.
    /// </summary>
    private string SelectedRate => SelectedSpeedIndex switch
    {
        0 => "-10%",   // Chậm
        1 => "+0%",    // Bình thường
        2 => "+20%",   // Nhanh
        _ => "+0%"
    };

    /// <summary>
    /// Copy transcript text to voiceover text for editing.
    /// </summary>
    public void PrepareVoiceoverFromTranscript()
    {
        if (!string.IsNullOrEmpty(TranscriptText))
        {
            VoiceoverText = TranscriptText;
        }
    }

    /// <summary>
    /// Translate the voiceover text to the selected language.
    /// </summary>
    public async Task TranslateVoiceoverTextAsync()
    {
        if (string.IsNullOrWhiteSpace(VoiceoverText))
        {
            ErrorMessage = "Không có nội dung để dịch.";
            return;
        }

        var targetLang = SelectedLanguage.TranslateCode;

        ClearMessages();
        IsTranslating = true;
        StatusMessage = $"🌐 Đang dịch sang {SelectedLanguage.DisplayName}...";

        try
        {
            var translated = await _translationService.TranslateAsync(
                VoiceoverText, "auto", targetLang);

            VoiceoverText = translated;
            StatusMessage = $"✅ Đã dịch sang {SelectedLanguage.Flag} {SelectedLanguage.DisplayName}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy dịch thuật.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsTranslating = false;
        }
    }

    /// <summary>
    /// Generate voiceover audio from text using Edge TTS.
    /// </summary>
    public async Task GenerateVoiceoverAsync()
    {
        if (string.IsNullOrWhiteSpace(VoiceoverText))
        {
            ErrorMessage = "Vui lòng nhập hoặc dán nội dung cần lồng tiếng.";
            return;
        }

        ClearMessages();
        VoiceoverSuccessMessage = string.Empty;
        VoiceoverFilePath = string.Empty;
        IsGeneratingVoiceover = true;
        VoiceoverProgressText = "Đang chuẩn bị...";
        _voiceoverCts = new CancellationTokenSource();

        // Wire up status events
        void OnStatus(string s) => Dispatcher.UIThread.Post(() => StatusMessage = s);
        _edgeTtsService.StatusChanged += OnStatus;

        try
        {
            // Determine output folder and base name
            var sourceFile = !string.IsNullOrEmpty(TranscribeFilePath) && File.Exists(TranscribeFilePath)
                ? TranscribeFilePath
                : (!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath)
                    ? DownloadedFilePath
                    : null);

            var outputFolder = sourceFile != null
                ? Path.GetDirectoryName(sourceFile) ?? SaveFolder
                : SaveFolder;

            var baseName = sourceFile != null
                ? Path.GetFileNameWithoutExtension(sourceFile)
                : "voiceover";

            // Parse voiceover text into paragraphs
            var paragraphs = VoiceoverText
                .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
                
            if (paragraphs.Count == 0)
                paragraphs.Add(VoiceoverText);

            var (outputPath, chunkDurations) = await _edgeTtsService.GenerateVoiceoverFromParagraphsAsync(
                paragraphs,
                SelectedVoiceName,
                SelectedRate,
                outputFolder,
                baseName,
                (current, total) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        VoiceoverProgressText = $"Đang tạo giọng nói... (đoạn {current}/{total})";
                    });
                },
                _voiceoverCts.Token
            );

            VoiceoverFilePath = outputPath;
            VoiceoverChunkDurations = chunkDurations;
            var langInfo = $"{SelectedLanguage.Flag} {SelectedLanguage.DisplayName}";
            VoiceoverSuccessMessage = $"✅ Lồng tiếng xong! [{langInfo}] File: {Path.GetFileName(outputPath)}";
            StatusMessage = $"✅ Voiceover saved to: {outputPath}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy tạo giọng nói.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsGeneratingVoiceover = false;
            VoiceoverProgressText = string.Empty;
            _edgeTtsService.StatusChanged -= OnStatus;
            _voiceoverCts?.Dispose();
            _voiceoverCts = null;
        }
    }

    /// <summary>
    /// Cancel voiceover generation.
    /// </summary>
    public void CancelVoiceover()
    {
        _voiceoverCts?.Cancel();
        _edgeTtsService.Cancel();
    }

    /// <summary>
    /// Play the generated voiceover audio using the system player.
    /// </summary>
    public void PlayVoiceover()
    {
        if (string.IsNullOrEmpty(VoiceoverFilePath) || !File.Exists(VoiceoverFilePath))
        {
            ErrorMessage = "Không tìm thấy file âm thanh.";
            return;
        }

        StopVoiceover();

        try
        {
            ProcessStartInfo psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = $"\"{VoiceoverFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start \"\" \"{VoiceoverFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{VoiceoverFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            _audioPlayerProcess = Process.Start(psi);
            IsPlayingAudio = true;

            // Monitor process in background to update IsPlayingAudio when done
            if (_audioPlayerProcess != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _audioPlayerProcess.WaitForExitAsync();
                    }
                    catch { }
                    finally
                    {
                        Dispatcher.UIThread.Post(() => IsPlayingAudio = false);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể phát audio: {ex.Message}";
            IsPlayingAudio = false;
        }
    }

    /// <summary>
    /// Stop the currently playing audio.
    /// </summary>
    public void StopVoiceover()
    {
        try
        {
            if (_audioPlayerProcess != null && !_audioPlayerProcess.HasExited)
            {
                _audioPlayerProcess.Kill(entireProcessTree: true);
            }
        }
        catch { }
        _audioPlayerProcess = null;
        IsPlayingAudio = false;
    }

    /// <summary>
    /// Open the folder containing the voiceover file.
    /// </summary>
    public void OpenVoiceoverFolder()
    {
        if (string.IsNullOrEmpty(VoiceoverFilePath) || !File.Exists(VoiceoverFilePath))
        {
            ErrorMessage = "Không tìm thấy file âm thanh.";
            return;
        }

        try
        {
            var path = Path.GetDirectoryName(VoiceoverFilePath) ?? SaveFolder;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("explorer.exe", path);
            else
                Process.Start("xdg-open", path);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not open folder: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════
    //  AI IMAGE GENERATION (Pollinations.AI — free or with API key)
    // ═══════════════════════════════════════════════════

    private readonly PollinationsService _pollinationsService = new();
    private CancellationTokenSource? _imageGenCts;

    private string _pollinationsApiKey = string.Empty;
    /// <summary>
    /// Pollinations API key (optional). 
    /// Empty = free mode (rate limited). 
    /// Key types: sk_ (secret) or pk_ (publishable).
    /// </summary>
    public string PollinationsApiKey
    {
        get => _pollinationsApiKey;
        set
        {
            _pollinationsApiKey = value?.Trim() ?? string.Empty;
            _pollinationsService.ApiKey = _pollinationsApiKey;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPollinationsApiKey));
            OnPropertyChanged(nameof(ApiKeyStatusText));
        }
    }

    public bool HasPollinationsApiKey => !string.IsNullOrWhiteSpace(PollinationsApiKey);

    public string ApiKeyStatusText => HasPollinationsApiKey
        ? "🔑 API Key đã cấu hình"
        : "🆓 Chế độ miễn phí (có thể bị giới hạn tốc độ)";

    private ObservableCollection<ImageGenerationItem> _imageItems = new();
    public ObservableCollection<ImageGenerationItem> ImageItems
    {
        get => _imageItems;
        set { _imageItems = value; OnPropertyChanged(); }
    }

    private bool _isGeneratingImages;
    public bool IsGeneratingImages
    {
        get => _isGeneratingImages;
        set
        {
            _isGeneratingImages = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGenerateImages));
            OnPropertyChanged(nameof(CanGoToRenderVideo));
        }
    }

    private string _imageGenProgressText = string.Empty;
    public string ImageGenProgressText
    {
        get => _imageGenProgressText;
        set { _imageGenProgressText = value; OnPropertyChanged(); }
    }

    private int _selectedImageStyleIndex;
    public int SelectedImageStyleIndex
    {
        get => _selectedImageStyleIndex;
        set { _selectedImageStyleIndex = value; OnPropertyChanged(); AutoSave(); }
    }

    private ImageStyle SelectedImageStyle => SelectedImageStyleIndex switch
    {
        0 => ImageStyle.Realistic,
        1 => ImageStyle.DigitalArt,
        2 => ImageStyle.FlatDesign,
        3 => ImageStyle.Cinematic,
        _ => ImageStyle.Realistic
    };

    private bool _showImageSection;
    public bool ShowImageSection
    {
        get => _showImageSection;
        set { _showImageSection = value; OnPropertyChanged(); AutoSave(); }
    }

    private bool _allImagesReady;
    public bool AllImagesReady
    {
        get => _allImagesReady;
        set
        {
            _allImagesReady = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoToRenderVideo));
            AutoSave();
        }
    }

    public bool CanGenerateImages => !IsGeneratingImages && HasTranscript;
    public bool CanGoToRenderVideo => AllImagesReady && !IsGeneratingImages;

    /// <summary>
    /// Parse transcript text into paragraphs for image generation.
    /// Prioritizes VoiceoverText (translated/edited) over TranscriptText (original).
    /// </summary>
    private List<string> ParseTranscriptParagraphs()
    {
        // Use VoiceoverText (translated/edited) if available, otherwise fall back to TranscriptText
        var sourceText = !string.IsNullOrWhiteSpace(VoiceoverText)
            ? VoiceoverText
            : TranscriptText;

        if (string.IsNullOrWhiteSpace(sourceText))
            return new List<string>();

        // Split by double newline (paragraph breaks)
        var paragraphs = sourceText
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        // Remove timestamps like [MM:SS] from each paragraph
        for (int i = 0; i < paragraphs.Count; i++)
        {
            paragraphs[i] = System.Text.RegularExpressions.Regex.Replace(
                paragraphs[i], @"\[\d{2}:\d{2}(:\d{2})?\]\s*", "").Trim();
        }

        return paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
    }

    /// <summary>
    /// Prepare image generation — parse transcript and show section.
    /// </summary>
    public void PrepareImageGeneration()
    {
        ShowImageSection = true;

        var paragraphs = ParseTranscriptParagraphs();
        ImageItems.Clear();
        AllImagesReady = false;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var item = new ImageGenerationItem
            {
                Index = i,
                ParagraphText = paragraphs[i],
                ImagePrompt = string.Empty
            };
            
            // Subscribed to changes (like user editing Prompt in UI) so we can auto-save
            item.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(ImageGenerationItem.ImagePrompt))
                    AutoSave();
            };
            
            ImageItems.Add(item);
        }
        AutoSave();
    }

    /// <summary>
    /// Get the output folder for generated images (subfolder named after the video).
    /// </summary>
    private string GetImageOutputFolder()
    {
        var sourceFile = !string.IsNullOrEmpty(TranscribeFilePath) && File.Exists(TranscribeFilePath)
            ? TranscribeFilePath
            : (!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath)
                ? DownloadedFilePath
                : null);

        var baseFolder = sourceFile != null
            ? Path.GetDirectoryName(sourceFile) ?? SaveFolder
            : SaveFolder;

        var videoName = sourceFile != null
            ? Path.GetFileNameWithoutExtension(sourceFile)
            : "images";

        // Sanitize folder name
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(videoName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

        var imageFolder = Path.Combine(baseFolder, $"{safeName}_images");

        if (!Directory.Exists(imageFolder))
        {
            Directory.CreateDirectory(imageFolder);
        }

        return imageFolder;
    }

    /// <summary>
    /// Generate all images: 
    /// Step 1 — auto-generate prompts from transcripts
    /// Step 2 — generate images sequentially with 1s delay
    /// </summary>
    public async Task GenerateAllImagesAsync()
    {
        if (ImageItems.Count == 0)
        {
            ErrorMessage = "Không có đoạn transcript nào để tạo ảnh.";
            return;
        }

        ClearMessages();
        IsGeneratingImages = true;
        AllImagesReady = false;
        _imageGenCts = new CancellationTokenSource();
        var ct = _imageGenCts.Token;
        var styleSuffix = SelectedImageStyle.GetSuffix();
        var imageFolder = GetImageOutputFolder();

        // Wire up status events
        void OnStatus(string s) => Dispatcher.UIThread.Post(() => StatusMessage = s);
        _pollinationsService.StatusChanged += OnStatus;

        try
        {
            var total = ImageItems.Count;

            // STEP 1: Generate prompts for items that don't have one
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = ImageItems[i];

                if (string.IsNullOrWhiteSpace(item.ImagePrompt))
                {
                    Dispatcher.UIThread.Post(() =>
                        ImageGenProgressText = $"Đang tạo prompt {i + 1}/{total}...");

                    var prompt = await _pollinationsService.GenerateImagePromptAsync(
                        item.ParagraphText, ct);

                    Dispatcher.UIThread.Post(() => {
                        item.ImagePrompt = prompt;
                        AutoSave();
                    });

                    // Small delay between prompt requests
                    await Task.Delay(500, ct);
                }
            }

            // STEP 2: Generate images sequentially
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = ImageItems[i];

                // Skip items that already have images
                if (item.HasImage && !item.HasError)
                    continue;

                Dispatcher.UIThread.Post(() =>
                {
                    ImageGenProgressText = $"Đang tạo ảnh {i + 1}/{total}...";
                    item.IsGenerating = true;
                    item.HasError = false;
                    item.ErrorMessage = string.Empty;
                });

                var outputPath = Path.Combine(imageFolder, $"image_{(i + 1):D3}.jpg");

                var success = await _pollinationsService.GenerateImageAsync(
                    item.ImagePrompt, styleSuffix, outputPath, ct);

                if (success)
                {
                    // Load bitmap on UI thread
                    var bytes = await File.ReadAllBytesAsync(outputPath, ct);
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            using var ms = new MemoryStream(bytes);
                            item.ImageBitmap = new Bitmap(ms);
                            item.ImagePath = outputPath;
                            item.HasError = false;
                            AutoSave();
                        }
                        catch
                        {
                            item.HasError = true;
                            item.ErrorMessage = "Không thể tải ảnh.";
                        }
                        item.IsGenerating = false;
                    });
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        item.HasError = true;
                        item.ErrorMessage = "Tạo ảnh thất bại. Bấm để thử lại.";
                        item.IsGenerating = false;
                    });
                }

                // 1 second delay between requests to avoid rate limiting
                if (i < total - 1)
                {
                    await Task.Delay(1000, ct);
                }
            }

            // Check if all images are ready
            CheckAllImagesReady();

            if (AllImagesReady)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ImageGenProgressText = string.Empty;
                    StatusMessage = $"✅ Tạo xong {total} ảnh! Sẵn sàng render video.";
                });
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ImageGenProgressText = string.Empty;
                    StatusMessage = "⚠️ Một số ảnh chưa tạo được. Bấm ảnh lỗi để thử lại.";
                });
            }
        }
        catch (OperationCanceledException)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ImageGenProgressText = string.Empty;
                StatusMessage = "Đã hủy tạo ảnh.";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ErrorMessage = ex.Message;
                ImageGenProgressText = string.Empty;
            });
        }
        finally
        {
            _pollinationsService.StatusChanged -= OnStatus;
            Dispatcher.UIThread.Post(() => IsGeneratingImages = false);
            _imageGenCts?.Dispose();
            _imageGenCts = null;
        }
    }

    /// <summary>
    /// Regenerate a single image (e.g. after user edited the prompt or clicked retry).
    /// </summary>
    public async Task RegenerateImageAsync(ImageGenerationItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ImagePrompt))
        {
            item.HasError = true;
            item.ErrorMessage = "Prompt không được để trống.";
            return;
        }

        item.IsGenerating = true;
        item.HasError = false;
        item.ErrorMessage = string.Empty;

        var styleSuffix = SelectedImageStyle.GetSuffix();
        var imageFolder = GetImageOutputFolder();
        var outputPath = Path.Combine(imageFolder, $"image_{(item.Index + 1):D3}.jpg");

        try
        {
            var success = await _pollinationsService.GenerateImageAsync(
                item.ImagePrompt, styleSuffix, outputPath);

            if (success)
            {
                var bytes = await File.ReadAllBytesAsync(outputPath);
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        using var ms = new MemoryStream(bytes);
                        item.ImageBitmap = new Bitmap(ms);
                        item.ImagePath = outputPath;
                        item.HasError = false;
                        AutoSave();
                    }
                    catch
                    {
                        item.HasError = true;
                        item.ErrorMessage = "Không thể tải ảnh.";
                    }
                    item.IsGenerating = false;
                });
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    item.HasError = true;
                    item.ErrorMessage = "Tạo ảnh thất bại. Bấm để thử lại.";
                    item.IsGenerating = false;
                });
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                item.HasError = true;
                item.ErrorMessage = $"Lỗi: {ex.Message}";
                item.IsGenerating = false;
            });
        }

        CheckAllImagesReady();
    }

    /// <summary>
    /// Cancel image generation.
    /// </summary>
    public void CancelImageGeneration()
    {
        _imageGenCts?.Cancel();
    }

    /// <summary>
    /// Check if all images have been generated successfully.
    /// </summary>
    private void CheckAllImagesReady()
    {
        Dispatcher.UIThread.Post(() =>
        {
            AllImagesReady = ImageItems.Count > 0 && ImageItems.All(i => i.HasImage && !i.HasError);
            OnPropertyChanged(nameof(CanGoToRenderVideo));
            OnPropertyChanged(nameof(CanStartRender));
        });
    }

    // ═══════════════════════════════════════════════════
    //  VIDEO PREVIEW & EDIT (before render)
    // ═══════════════════════════════════════════════════

    private CancellationTokenSource? _previewPlayCts;
    private Process? _previewAudioProcess;

    // ── Subtitle Style ──
    private SubtitleStyle _subtitleStyle = new();
    public SubtitleStyle SubtitleStyleSettings
    {
        get => _subtitleStyle;
        set
        {
            if (_subtitleStyle != null)
                _subtitleStyle.PropertyChanged -= OnSubtitleStyleChanged;
            _subtitleStyle = value;
            if (_subtitleStyle != null)
                _subtitleStyle.PropertyChanged += OnSubtitleStyleChanged;
            OnPropertyChanged();
        }
    }

    private void OnSubtitleStyleChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshPreviewFrame();
        AutoSave();
    }

    public void InitSubtitleStyleEvents()
    {
        _subtitleStyle.PropertyChanged += OnSubtitleStyleChanged;
    }

    // ── Preview Frame (composite: image + subtitle burnt on) ──
    private Bitmap? _previewFrameBitmap;
    public Bitmap? PreviewFrameBitmap
    {
        get => _previewFrameBitmap;
        set { _previewFrameBitmap = value; OnPropertyChanged(); }
    }

    private int _previewCurrentIndex;
    public int PreviewCurrentIndex
    {
        get => _previewCurrentIndex;
        set
        {
            if (ImageItems.Count == 0) return;
            _previewCurrentIndex = Math.Clamp(value, 0, ImageItems.Count - 1);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewDurationText));
            OnPropertyChanged(nameof(PreviewPositionText));
            OnPropertyChanged(nameof(CanPreviewPrev));
            OnPropertyChanged(nameof(CanPreviewNext));
            UpdatePreviewSubtitle();
        }
    }

    private string _previewSubtitleText = string.Empty;
    public string PreviewSubtitleText
    {
        get => _previewSubtitleText;
        set
        {
            _previewSubtitleText = value;
            OnPropertyChanged();
            RefreshPreviewFrame();
        }
    }

    private void RefreshPreviewFrame()
    {
        if (ImageItems.Count == 0 || _previewCurrentIndex >= ImageItems.Count)
        {
            PreviewFrameBitmap = null;
            return;
        }

        var item = ImageItems[_previewCurrentIndex];
        if (string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath))
        {
            PreviewFrameBitmap = item.ImageBitmap;
            return;
        }

        try
        {
            using var skBitmap = SubtitleRenderer.GeneratePreviewFrame(
                item.ImagePath, _previewSubtitleText, 960, 540, _subtitleStyle);

            if (skBitmap != null)
            {
                using var data = skBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
                using var ms = new MemoryStream();
                data.SaveTo(ms);
                ms.Position = 0;
                PreviewFrameBitmap = new Bitmap(ms);
            }
        }
        catch
        {
            PreviewFrameBitmap = item.ImageBitmap;
        }
    }

    public string PreviewDurationText
    {
        get
        {
            if (VoiceoverChunkDurations.Count > _previewCurrentIndex)
                return $"{VoiceoverChunkDurations[_previewCurrentIndex]:F1}s";
            return "—";
        }
    }

    public string PreviewPositionText =>
        ImageItems.Count > 0
            ? $"{_previewCurrentIndex + 1} / {ImageItems.Count}"
            : "0 / 0";

    // ── Timeline ──
    private double _previewTimelineProgress;
    public double PreviewTimelineProgress
    {
        get => _previewTimelineProgress;
        set { _previewTimelineProgress = value; OnPropertyChanged(); }
    }

    private string _previewTimeText = "00:00 / 00:00";
    public string PreviewTimeText
    {
        get => _previewTimeText;
        set { _previewTimeText = value; OnPropertyChanged(); }
    }

    public bool CanPreviewPrev => _previewCurrentIndex > 0;
    public bool CanPreviewNext => _previewCurrentIndex < ImageItems.Count - 1;
    public bool HasPreviewContent => AllImagesReady && ImageItems.Count > 0;

    private bool _isPreviewPlaying;
    public bool IsPreviewPlaying
    {
        get => _isPreviewPlaying;
        set { _isPreviewPlaying = value; OnPropertyChanged(); }
    }

    // ── Subtitle Display Mode ──

    private int _selectedSubtitleModeIndex;
    public int SelectedSubtitleModeIndex
    {
        get => _selectedSubtitleModeIndex;
        set
        {
            _selectedSubtitleModeIndex = value;
            OnPropertyChanged();
            UpdatePreviewSubtitle();
            AutoSave();
        }
    }

    public SubtitleDisplayMode SelectedSubtitleMode => SelectedSubtitleModeIndex switch
    {
        0 => SubtitleDisplayMode.FullText,
        1 => SubtitleDisplayMode.SentenceBySentence,
        2 => SubtitleDisplayMode.ShortLines,
        3 => SubtitleDisplayMode.WordByWord,
        _ => SubtitleDisplayMode.FullText
    };

    // ── Preset Color Setters ──
    public void SetFontColor(string hex) { _subtitleStyle.FontColor = hex; }
    public void SetOutlineColor(string hex) { _subtitleStyle.OutlineColor = hex; }

    private void UpdatePreviewSubtitle()
    {
        if (ImageItems.Count == 0 || _previewCurrentIndex >= ImageItems.Count)
        {
            PreviewSubtitleText = string.Empty;
            return;
        }

        var fullText = ImageItems[_previewCurrentIndex].ParagraphText;
        if (SelectedSubtitleMode == SubtitleDisplayMode.FullText)
            PreviewSubtitleText = fullText;
        else
        {
            var chunks = SubtitleTextSplitter.SplitText(fullText, SelectedSubtitleMode);
            PreviewSubtitleText = chunks.FirstOrDefault() ?? fullText;
        }
    }

    public void PreviewGoTo(int index) => PreviewCurrentIndex = index;
    public void PreviewNext() { if (CanPreviewNext) PreviewCurrentIndex++; }
    public void PreviewPrev() { if (CanPreviewPrev) PreviewCurrentIndex--; }

    public void StartPreviewSlideshow()
    {
        if (ImageItems.Count == 0 || !AllImagesReady) return;
        StopPreviewSlideshow();

        IsPreviewPlaying = true;
        PreviewCurrentIndex = 0;
        PreviewTimelineProgress = 0;
        _previewPlayCts = new CancellationTokenSource();
        var ct = _previewPlayCts.Token;

        StartPreviewAudio();

        var totalDuration = VoiceoverChunkDurations.Count > 0
            ? VoiceoverChunkDurations.Sum() : ImageItems.Count * 3.0;
        var totalFormatted = FormatPreviewTime(totalDuration);

        Task.Run(async () =>
        {
            try
            {
                double elapsed = 0;

                for (int i = 0; i < ImageItems.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    Dispatcher.UIThread.Post(() => PreviewCurrentIndex = i);

                    var duration = (VoiceoverChunkDurations.Count > i)
                        ? VoiceoverChunkDurations[i] : 3.0;
                    var fullText = ImageItems[i].ParagraphText;
                    var mode = SelectedSubtitleMode;

                    if (mode == SubtitleDisplayMode.FullText)
                    {
                        Dispatcher.UIThread.Post(() => PreviewSubtitleText = fullText);
                        var totalMs = Math.Max(500, (int)(duration * 1000));
                        for (int t = 0; t < totalMs; t += 100)
                        {
                            ct.ThrowIfCancellationRequested();
                            var ce = elapsed + t / 1000.0;
                            Dispatcher.UIThread.Post(() =>
                            {
                                PreviewTimelineProgress = (ce / totalDuration) * 100;
                                PreviewTimeText = $"{FormatPreviewTime(ce)} / {totalFormatted}";
                            });
                            await Task.Delay(100, ct);
                        }
                        elapsed += duration;
                    }
                    else
                    {
                        var chunks = SubtitleTextSplitter.SplitText(fullText, mode);
                        var chunkDuration = duration / Math.Max(1, chunks.Count);

                        for (int c = 0; c < chunks.Count; c++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var chunkText = chunks[c];
                            Dispatcher.UIThread.Post(() => PreviewSubtitleText = chunkText);
                            var chunkMs = Math.Max(300, (int)(chunkDuration * 1000));
                            for (int t = 0; t < chunkMs; t += 100)
                            {
                                ct.ThrowIfCancellationRequested();
                                var ce = elapsed + t / 1000.0;
                                Dispatcher.UIThread.Post(() =>
                                {
                                    PreviewTimelineProgress = (ce / totalDuration) * 100;
                                    PreviewTimeText = $"{FormatPreviewTime(ce)} / {totalFormatted}";
                                });
                                await Task.Delay(100, ct);
                            }
                            elapsed += chunkDuration;
                        }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    PreviewTimelineProgress = 100;
                    PreviewTimeText = $"{totalFormatted} / {totalFormatted}";
                });
            }
            catch (OperationCanceledException) { }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsPreviewPlaying = false;
                    StopPreviewAudio();
                });
            }
        });
    }

    private static string FormatPreviewTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    public void StopPreviewSlideshow()
    {
        _previewPlayCts?.Cancel();
        _previewPlayCts?.Dispose();
        _previewPlayCts = null;
        IsPreviewPlaying = false;
        StopPreviewAudio();
    }

    private void StartPreviewAudio()
    {
        StopPreviewAudio();
        if (!HasVoiceover || string.IsNullOrEmpty(VoiceoverFilePath) || !File.Exists(VoiceoverFilePath))
            return;
        try
        {
            ProcessStartInfo? psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                psi = new ProcessStartInfo { FileName = "afplay", Arguments = $"\"{VoiceoverFilePath}\"", UseShellExecute = false, CreateNoWindow = true };
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                psi = new ProcessStartInfo { FileName = "cmd", Arguments = $"/c start \"\" \"{VoiceoverFilePath}\"", UseShellExecute = false, CreateNoWindow = true };
            else
                psi = new ProcessStartInfo { FileName = "xdg-open", Arguments = $"\"{VoiceoverFilePath}\"", UseShellExecute = false, CreateNoWindow = true };
            _previewAudioProcess = Process.Start(psi);
        }
        catch { }
    }

    private void StopPreviewAudio()
    {
        try { if (_previewAudioProcess != null && !_previewAudioProcess.HasExited) _previewAudioProcess.Kill(entireProcessTree: true); } catch { }
        _previewAudioProcess = null;
    }

    // ── Segment Reorder ──

    public void MoveSegmentUp(int index)
    {
        if (index <= 0 || index >= ImageItems.Count) return;
        SwapSegments(index, index - 1);
    }

    public void MoveSegmentDown(int index)
    {
        if (index < 0 || index >= ImageItems.Count - 1) return;
        SwapSegments(index, index + 1);
    }

    private void SwapSegments(int a, int b)
    {
        (ImageItems[a], ImageItems[b]) = (ImageItems[b], ImageItems[a]);
        ImageItems[a].Index = a;
        ImageItems[b].Index = b;
        if (VoiceoverChunkDurations.Count > Math.Max(a, b))
        {
            (VoiceoverChunkDurations[a], VoiceoverChunkDurations[b]) =
                (VoiceoverChunkDurations[b], VoiceoverChunkDurations[a]);
        }
        UpdatePreviewSubtitle();
        OnPropertyChanged(nameof(PreviewDurationText));
        AutoSave();
    }

    public void InitializePreview()
    {
        if (ImageItems.Count > 0)
        {
            PreviewCurrentIndex = 0;
            OnPropertyChanged(nameof(HasPreviewContent));
            RefreshPreviewFrame();
        }
    }


    // ═══════════════════════════════════════════════════
    //  VIDEO RENDER (ffmpeg — 100% offline)
    // ═══════════════════════════════════════════════════

    private readonly VideoRenderService _videoRenderService = new();
    private CancellationTokenSource? _renderCts;

    private bool _showRenderSection;
    public bool ShowRenderSection
    {
        get => _showRenderSection;
        set { _showRenderSection = value; OnPropertyChanged(); AutoSave(); }
    }

    private bool _isRendering;
    public bool IsRendering
    {
        get => _isRendering;
        set
        {
            _isRendering = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanStartRender));
        }
    }

    private double _renderProgress;
    public double RenderProgress
    {
        get => _renderProgress;
        set { _renderProgress = value; OnPropertyChanged(); }
    }

    private string _renderProgressText = string.Empty;
    public string RenderProgressText
    {
        get => _renderProgressText;
        set { _renderProgressText = value; OnPropertyChanged(); }
    }

    private int _selectedResolutionIndex; // 0 = 1920x1080, 1 = 1280x720
    public int SelectedResolutionIndex
    {
        get => _selectedResolutionIndex;
        set { _selectedResolutionIndex = value; OnPropertyChanged(); AutoSave(); }
    }

    private (int width, int height) SelectedResolution => SelectedResolutionIndex switch
    {
        0 => (1920, 1080),
        1 => (1280, 720),
        _ => (1920, 1080)
    };

    private string _renderedVideoPath = string.Empty;
    public string RenderedVideoPath
    {
        get => _renderedVideoPath;
        set
        {
            _renderedVideoPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasRenderedVideo));
            AutoSave();
        }
    }

    public bool HasRenderedVideo => !string.IsNullOrEmpty(RenderedVideoPath) && File.Exists(RenderedVideoPath);

    private string _renderedVideoInfo = string.Empty;
    public string RenderedVideoInfo
    {
        get => _renderedVideoInfo;
        set { _renderedVideoInfo = value; OnPropertyChanged(); }
    }

    private Bitmap? _renderedVideoThumbnail;
    public Bitmap? RenderedVideoThumbnail
    {
        get => _renderedVideoThumbnail;
        set
        {
            _renderedVideoThumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasRenderedVideoThumbnail));
        }
    }

    public bool HasRenderedVideoThumbnail => RenderedVideoThumbnail != null;

    public bool CanStartRender => !IsRendering && AllImagesReady && HasVoiceover;

    /// <summary>
    /// Navigate to the render section.
    /// </summary>
    public void GoToRenderSection()
    {
        ShowRenderSection = true;
        OnPropertyChanged(nameof(CanStartRender));
        InitializePreview();
    }

    /// <summary>
    /// Render the final video from images + voiceover.
    /// </summary>
    public async Task RenderVideoAsync()
    {
        if (!AllImagesReady)
        {
            ErrorMessage = "Chưa tạo xong tất cả ảnh.";
            return;
        }

        if (!HasVoiceover)
        {
            ErrorMessage = "Chưa có file voiceover. Vui lòng tạo lồng tiếng trước.";
            return;
        }

        ClearMessages();
        IsRendering = true;
        RenderProgress = 0;
        RenderProgressText = "Đang chuẩn bị render...";
        RenderedVideoPath = string.Empty;
        RenderedVideoInfo = string.Empty;
        RenderedVideoThumbnail = null;
        _renderCts = new CancellationTokenSource();
        var ct = _renderCts.Token;

        // Wire up events
        void OnStatus(string s) => Dispatcher.UIThread.Post(() => StatusMessage = s);
        void OnProgress(double percent, string text) => Dispatcher.UIThread.Post(() =>
        {
            RenderProgress = percent;
            RenderProgressText = text;
        });

        _videoRenderService.StatusChanged += OnStatus;
        _videoRenderService.ProgressChanged += OnProgress;

        try
        {
            // Gather image paths and paragraph texts
            var imagePaths = ImageItems
                .OrderBy(i => i.Index)
                .Select(i => i.ImagePath)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .ToList();

            var paragraphTexts = ImageItems
                .OrderBy(i => i.Index)
                .Select(i => i.ParagraphText)
                .ToList();

            // Determine output path
            var sourceFile = !string.IsNullOrEmpty(TranscribeFilePath) && File.Exists(TranscribeFilePath)
                ? TranscribeFilePath
                : (!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath)
                    ? DownloadedFilePath
                    : null);

            var outputFolder = sourceFile != null
                ? Path.GetDirectoryName(sourceFile) ?? SaveFolder
                : SaveFolder;

            var baseName = sourceFile != null
                ? Path.GetFileNameWithoutExtension(sourceFile)
                : "video";

            // Sanitize
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = new string(baseName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            if (safeName.Length > 80) safeName = safeName[..80];

            var outputPath = Path.Combine(outputFolder, $"{safeName}_VN.mp4");

            // Avoid overwriting
            var counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(outputFolder, $"{safeName}_VN_{counter}.mp4");
                counter++;
            }

            var (w, h) = SelectedResolution;

            await _videoRenderService.RenderVideoAsync(
                imagePaths, paragraphTexts, VoiceoverChunkDurations, VoiceoverFilePath,
                outputPath, w, h, SelectedSubtitleMode, _subtitleStyle, ct);

            RenderedVideoPath = outputPath;

            // Get video info
            var (duration, fileSize) = await _videoRenderService.GetVideoInfoAsync(outputPath, ct);
            var durationStr = VideoRenderService.FormatFileSize(fileSize);
            var durationTime = TimeSpan.FromSeconds(duration);
            var durationFormatted = durationTime.TotalHours >= 1
                ? $"{(int)durationTime.TotalHours:D2}:{durationTime.Minutes:D2}:{durationTime.Seconds:D2}"
                : $"{durationTime.Minutes:D2}:{durationTime.Seconds:D2}";

            RenderedVideoInfo = $"📐 {w}x{h} • ⏱ {durationFormatted} • 💾 {durationStr}";

            // Load first image as thumbnail
            if (imagePaths.Count > 0 && File.Exists(imagePaths[0]))
            {
                try
                {
                    var thumbBytes = await File.ReadAllBytesAsync(imagePaths[0], ct);
                    using var ms = new MemoryStream(thumbBytes);
                    RenderedVideoThumbnail = new Bitmap(ms);
                }
                catch { }
            }

            Dispatcher.UIThread.Post(() =>
            {
                RenderProgress = 100;
                RenderProgressText = "Hoàn tất!";
                StatusMessage = $"✅ Video đã render xong: {Path.GetFileName(outputPath)}";
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.UIThread.Post(() =>
            {
                RenderProgressText = string.Empty;
                StatusMessage = "Đã hủy render video.";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ErrorMessage = ex.Message;
                RenderProgressText = string.Empty;
            });
        }
        finally
        {
            _videoRenderService.StatusChanged -= OnStatus;
            _videoRenderService.ProgressChanged -= OnProgress;
            Dispatcher.UIThread.Post(() => IsRendering = false);
            _renderCts?.Dispose();
            _renderCts = null;
        }
    }

    /// <summary>
    /// Cancel the current render.
    /// </summary>
    public void CancelRender()
    {
        _renderCts?.Cancel();
        _videoRenderService.Cancel();
    }

    /// <summary>
    /// Preview the rendered video using the system's default video player.
    /// </summary>
    public void PreviewRenderedVideo()
    {
        if (string.IsNullOrEmpty(RenderedVideoPath) || !File.Exists(RenderedVideoPath))
        {
            ErrorMessage = "Không tìm thấy file video.";
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", $"\"{RenderedVideoPath}\"");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = RenderedVideoPath, UseShellExecute = true });
            else
                Process.Start("xdg-open", $"\"{RenderedVideoPath}\"");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể mở video: {ex.Message}";
        }
    }

    /// <summary>
    /// Open the folder containing the rendered video.
    /// </summary>
    public void OpenRenderedVideoFolder()
    {
        if (string.IsNullOrEmpty(RenderedVideoPath) || !File.Exists(RenderedVideoPath))
        {
            ErrorMessage = "Không tìm thấy file video.";
            return;
        }

        try
        {
            var path = Path.GetDirectoryName(RenderedVideoPath) ?? SaveFolder;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("explorer.exe", path);
            else
                Process.Start("xdg-open", path);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể mở thư mục: {ex.Message}";
        }
    }

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
