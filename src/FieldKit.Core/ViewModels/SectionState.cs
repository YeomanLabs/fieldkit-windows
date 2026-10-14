using FieldKit.Core.Domain;

namespace FieldKit.Core.ViewModels;

/// <summary>Load state for one page section. Sections load and fail independently.</summary>
public sealed record SectionState<T> : ISectionState
{
    private SectionState(SectionKind kind, T? value = default, string? message = null)
    {
        Kind = kind;
        Value = value;
        Message = message;
    }

    public SectionKind Kind { get; }

    /// <summary>The value when <see cref="Kind"/> is <see cref="SectionKind.Loaded"/>.</summary>
    public T? Value { get; }

    public string? Message { get; }

    public static SectionState<T> Loading { get; } = new(SectionKind.Loading);

    public static SectionState<T> NotFound { get; } = new(SectionKind.NotFound);

    public static SectionState<T> Loaded(T value) => new(SectionKind.Loaded, value);

    public static SectionState<T> Skipped(string reason) => new(SectionKind.Skipped, message: reason);

    public static SectionState<T> Failed(string message) => new(SectionKind.Failed, message: message);

    public bool IsLoading => Kind == SectionKind.Loading;

    /// <summary>How flag evaluation sees this section.</summary>
    public Lookup<T> ToLookup() =>
        Kind switch
        {
            SectionKind.Loaded => Lookup<T>.Found(Value!),
            SectionKind.NotFound => Lookup<T>.NotFound,
            SectionKind.Skipped => Lookup<T>.Skipped,
            _ => Lookup<T>.Unavailable,
        };
}
