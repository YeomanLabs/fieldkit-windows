namespace FieldKit.Core.ViewModels;

/// <summary>One label and value on the hardware, storage and apps tab.</summary>
public sealed record InfoRow(string Label, string Value, InfoTone Tone = InfoTone.Normal);
