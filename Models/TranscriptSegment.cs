namespace AutomationContent.Models;

/// <summary>
/// Represents a single segment of a transcript with timestamp.
/// </summary>
public class TranscriptSegment
{
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Format timestamp as [MM:SS]
    /// </summary>
    public string FormattedTimestamp
    {
        get
        {
            var ts = TimeSpan.FromSeconds(StartSeconds);
            return ts.TotalHours >= 1
                ? $"[{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}]"
                : $"[{ts.Minutes:D2}:{ts.Seconds:D2}]";
        }
    }
}

/// <summary>
/// Holds the full transcript result from Whisper API.
/// </summary>
public class TranscriptResult
{
    public string FullText { get; set; } = string.Empty;
    public List<TranscriptSegment> Segments { get; set; } = new();
    public string DetectedLanguage { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Formats the transcript with timestamps, grouped into paragraphs by ~5 second pauses.
    /// </summary>
    public string ToFormattedText()
    {
        if (Segments.Count == 0)
            return FullText;

        var paragraphs = new List<(double timestamp, List<string> texts)>();
        var currentTexts = new List<string>();
        var currentTimestamp = Segments[0].StartSeconds;

        for (int i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];

            // Start a new paragraph if gap >= 5 seconds from the previous segment's end
            if (i > 0 && segment.StartSeconds - Segments[i - 1].EndSeconds >= 5.0 && currentTexts.Count > 0)
            {
                paragraphs.Add((currentTimestamp, new List<string>(currentTexts)));
                currentTexts.Clear();
                currentTimestamp = segment.StartSeconds;
            }
            // Also start a new paragraph every ~30 seconds to avoid very long paragraphs
            else if (currentTexts.Count > 0 && segment.StartSeconds - currentTimestamp >= 30.0)
            {
                paragraphs.Add((currentTimestamp, new List<string>(currentTexts)));
                currentTexts.Clear();
                currentTimestamp = segment.StartSeconds;
            }

            currentTexts.Add(segment.Text.Trim());
        }

        // Add the last paragraph
        if (currentTexts.Count > 0)
        {
            paragraphs.Add((currentTimestamp, currentTexts));
        }

        var lines = new List<string>();
        foreach (var (timestamp, texts) in paragraphs)
        {
            var ts = TimeSpan.FromSeconds(timestamp);
            var tsStr = ts.TotalHours >= 1
                ? $"[{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}]"
                : $"[{ts.Minutes:D2}:{ts.Seconds:D2}]";
            lines.Add($"{tsStr} {string.Join(" ", texts)}");
        }

        return string.Join("\n\n", lines);
    }
}
