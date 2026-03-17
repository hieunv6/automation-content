using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutomationContent.Models;

/// <summary>
/// Vertical position of subtitle text on the frame.
/// </summary>
public enum SubtitlePosition
{
    Bottom,
    Center,
    Top
}

/// <summary>
/// Holds all subtitle styling options for the video.
/// </summary>
public class SubtitleStyle : INotifyPropertyChanged
{
    // ── Font Size ──
    private float _fontSize = 50f;
    public float FontSize
    {
        get => _fontSize;
        set { _fontSize = value; OnPropertyChanged(); }
    }

    // ── Font Color (hex) ──
    private string _fontColor = "#FFFFFF";
    public string FontColor
    {
        get => _fontColor;
        set { _fontColor = value; OnPropertyChanged(); }
    }

    // ── Outline Color (hex) ──
    private string _outlineColor = "#000000";
    public string OutlineColor
    {
        get => _outlineColor;
        set { _outlineColor = value; OnPropertyChanged(); }
    }

    // ── Outline Width ──
    private float _outlineWidth = 6f;
    public float OutlineWidth
    {
        get => _outlineWidth;
        set { _outlineWidth = value; OnPropertyChanged(); }
    }

    // ── Position ──
    private int _positionIndex; // 0=Bottom, 1=Center, 2=Top
    public int PositionIndex
    {
        get => _positionIndex;
        set { _positionIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(Position)); }
    }

    public SubtitlePosition Position => PositionIndex switch
    {
        0 => SubtitlePosition.Bottom,
        1 => SubtitlePosition.Center,
        2 => SubtitlePosition.Top,
        _ => SubtitlePosition.Bottom
    };

    // ── Vertical Margin (px from edge) ──
    private float _verticalMargin = 80f;
    public float VerticalMargin
    {
        get => _verticalMargin;
        set { _verticalMargin = value; OnPropertyChanged(); }
    }

    // ── Background Box ──
    private bool _showBackground;
    public bool ShowBackground
    {
        get => _showBackground;
        set { _showBackground = value; OnPropertyChanged(); }
    }

    private string _backgroundColor = "#80000000"; // semi-transparent black
    public string BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
