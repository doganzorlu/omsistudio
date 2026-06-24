using System.Collections.Generic;

namespace OmsiStudio.Core.Conversion;

public sealed record ConversionResult
{
    public ConversionStatus Status { get; init; } = ConversionStatus.Unknown;
    public IReadOnlyList<string> OutputFiles { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}
