namespace FieldKit.Core.Setup;

/// <summary>One step of the Entra registration guide.</summary>
public sealed record SetupStep(int Number, string Title, string Detail, IReadOnlyList<SetupItem> Items);
