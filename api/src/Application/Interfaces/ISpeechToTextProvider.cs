namespace ZahSellerAI.Application.Interfaces;

public class SpeechToTextResult
{
    public string OriginalText { get; set; } = string.Empty;
    public string DetectedLanguage { get; set; } = "ta";
    public double Confidence { get; set; }
}

public interface ISpeechToTextProvider
{
    Task<SpeechToTextResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default
    );
}

public class TranslationResult
{
    public string TranslatedText { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "ta";
    public string TargetLanguage { get; set; } = "en";
}

public interface ITranslationProvider
{
    Task<TranslationResult> TranslateAsync(
        string text,
        string sourceLanguage = "ta",
        string targetLanguage = "en",
        CancellationToken cancellationToken = default
    );
}
