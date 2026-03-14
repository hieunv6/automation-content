using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace AutomationContent.Models;

/// <summary>
/// Represents one paragraph → image mapping for AI image generation.
/// </summary>
public class ImageGenerationItem : INotifyPropertyChanged
{
    public int Index { get; set; }

    private string _paragraphText = string.Empty;
    public string ParagraphText
    {
        get => _paragraphText;
        set { _paragraphText = value; OnPropertyChanged(); }
    }

    private string _imagePrompt = string.Empty;
    public string ImagePrompt
    {
        get => _imagePrompt;
        set { _imagePrompt = value; OnPropertyChanged(); }
    }

    private string _imagePath = string.Empty;
    public string ImagePath
    {
        get => _imagePath;
        set
        {
            _imagePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
        }
    }

    private Bitmap? _imageBitmap;
    public Bitmap? ImageBitmap
    {
        get => _imageBitmap;
        set
        {
            _imageBitmap = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => ImageBitmap != null;

    private bool _isGenerating;
    public bool IsGenerating
    {
        get => _isGenerating;
        set { _isGenerating = value; OnPropertyChanged(); }
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set { _hasError = value; OnPropertyChanged(); }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Display label like "1/12"
    /// </summary>
    public string DisplayLabel => $"{Index + 1}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Image style presets that get appended to every prompt.
/// </summary>
public enum ImageStyle
{
    Realistic,
    DigitalArt,
    FlatDesign,
    Cinematic
}

public static class ImageStyleExtensions
{
    public static string GetSuffix(this ImageStyle style) => style switch
    {
        ImageStyle.Realistic => ", professional photography, 4K",
        ImageStyle.DigitalArt => ", digital illustration, vibrant colors",
        ImageStyle.FlatDesign => ", minimalist flat vector art",
        ImageStyle.Cinematic => ", cinematic lighting, dramatic scene",
        _ => ", professional photography, 4K"
    };

    public static string GetDisplayName(this ImageStyle style) => style switch
    {
        ImageStyle.Realistic => "📷 Realistic photo",
        ImageStyle.DigitalArt => "🎨 Digital art",
        ImageStyle.FlatDesign => "📐 Flat design",
        ImageStyle.Cinematic => "🎬 Cinematic",
        _ => "📷 Realistic photo"
    };
}
