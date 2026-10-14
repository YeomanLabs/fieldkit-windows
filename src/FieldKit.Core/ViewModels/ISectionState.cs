namespace FieldKit.Core.ViewModels;

/// <summary>What a section card needs to show a <see cref="SectionState{T}"/> that isn't loaded.</summary>
public interface ISectionState
{
    SectionKind Kind { get; }

    /// <summary>The skip reason or failure message.</summary>
    string? Message { get; }
}
