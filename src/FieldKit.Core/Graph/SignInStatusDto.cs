namespace FieldKit.Core.Graph;

/// <summary>A sign-in's <c>status</c>: 0 for success, otherwise the AADSTS number and Entra's reason.</summary>
internal sealed class SignInStatusDto
{
    public int? ErrorCode { get; init; }

    public string? FailureReason { get; init; }

    public string? AdditionalDetails { get; init; }
}
