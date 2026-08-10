namespace VoidNote.Shawzin.Codec;

/// <summary>Classifies a Shawzin codec validation or parsing failure.</summary>
public enum ShawzinCodeErrorCategory
{
    /// <summary>No code or song was supplied.</summary>
    EmptyInput,
    /// <summary>The scale header is missing or unsupported.</summary>
    InvalidScale,
    /// <summary>The code does not contain complete fixed-width events.</summary>
    TruncatedCode,
    /// <summary>A symbol is outside the format alphabet.</summary>
    InvalidCharacter,
    /// <summary>A note symbol does not strike any string.</summary>
    InvalidNoteSymbol,
    /// <summary>A timestamp symbol is invalid.</summary>
    InvalidTimingSymbol,
    /// <summary>Events are not strictly chronological.</summary>
    InvalidEventOrder,
    /// <summary>A timestamp lies outside the format range.</summary>
    InvalidTiming,
    /// <summary>The event count exceeds the structural format limit.</summary>
    EventLimitExceeded,
    /// <summary>The internal song model is inconsistent.</summary>
    InvalidModel,
    /// <summary>Two distinct positions collapse onto one format timestamp.</summary>
    QuantizationCollision,
}

/// <summary>Describes one codec error without exposing a parser stack trace.</summary>
public sealed record ShawzinCodeError(
    ShawzinCodeErrorCategory Category,
    string Description,
    int? CodePosition = null,
    char? Symbol = null,
    int? EventIndex = null);

/// <summary>Contains the errors found by a Shawzin validation operation.</summary>
public sealed record ShawzinValidationResult(IReadOnlyList<ShawzinCodeError> Errors)
{
    /// <summary>Gets whether validation completed without errors.</summary>
    public bool IsValid => Errors.Count == 0;
}
