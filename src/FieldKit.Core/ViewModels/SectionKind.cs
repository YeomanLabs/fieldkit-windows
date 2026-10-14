namespace FieldKit.Core.ViewModels;

/// <summary>The cases of <see cref="SectionState{T}"/>.</summary>
public enum SectionKind
{
    Loading,
    Loaded,
    NotFound,
    Skipped,
    Failed,
}
