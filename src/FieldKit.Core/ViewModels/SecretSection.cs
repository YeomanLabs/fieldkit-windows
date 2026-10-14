namespace FieldKit.Core.ViewModels;

/// <summary>A group of rows in the secrets dialog: plain details, then the secrets, with an optional header and footer.</summary>
public sealed record SecretSection(
    string Header,
    IReadOnlyList<SecretDetail> Details,
    IReadOnlyList<SecretValue> Secrets,
    string Footer)
{
    public bool HasHeader => Header.Length > 0;

    public bool HasFooter => Footer.Length > 0;
}
