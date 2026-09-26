using ZahSellerAI.Domain.Enums;

namespace ZahSellerAI.Domain.ValueObjects;

public class SourcedValue<T>
{
    public T? Value { get; set; }
    public InformationSource Source { get; set; }
    public double Confidence { get; set; } = 1.0;
    public string? OriginalValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SourcedValue() { }

    public SourcedValue(T? value, InformationSource source, double confidence = 1.0)
    {
        Value = value;
        Source = source;
        Confidence = confidence;
    }

    public bool IsMissing => Value == null || (Value is string str && string.IsNullOrWhiteSpace(str));
    public bool IsConfirmed => Confidence >= 0.9 && (Source == InformationSource.SellerInput || Source == InformationSource.SellerVoice);
    public bool IsInferred => Source == InformationSource.AIInference || Source == InformationSource.Vision;
}
