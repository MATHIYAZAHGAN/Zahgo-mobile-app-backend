using ZahSellerAI.Domain.Enums;

namespace ZahSellerAI.Application.Interfaces;

/// <summary>
/// Interface for speech-to-text conversion
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// Transcribes audio to text with language detection
    /// </summary>
    Task<SpeechTranscriptionResult> TranscribeAsync(
        Stream audioStream, 
        string fileName, 
        Language? expectedLanguage = null);

    /// <summary>
    /// Detects language from audio
    /// </summary>
    Task<Language> DetectLanguageAsync(Stream audioStream);
}

public class SpeechTranscriptionResult
{
    public string TranscribedText { get; set; } = string.Empty;
    public Language DetectedLanguage { get; set; }
    public double Confidence { get; set; }
    public TimeSpan Duration { get; set; }
    public List<TranscriptSegment> Segments { get; set; } = new();
}

public class TranscriptSegment
{
    public string Text { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public double Confidence { get; set; }
}
