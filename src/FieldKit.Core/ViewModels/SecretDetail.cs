namespace FieldKit.Core.ViewModels;

/// <summary>A plain label and value in the secrets dialog (device, account, dates, key ID).</summary>
public sealed record SecretDetail(string Label, string Value);
