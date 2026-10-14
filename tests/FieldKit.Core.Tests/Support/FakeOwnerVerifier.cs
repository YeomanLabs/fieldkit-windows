using FieldKit.Core.Services;

namespace FieldKit.Core.Tests.Support;

/// <summary>Ports <c>FakeOwnerAuthenticator</c>: records each reason and answers with <see cref="Failure"/>.</summary>
internal sealed class FakeOwnerVerifier(string? failure = null) : IOwnerVerifier
{
    public string? Failure { get; set; } = failure;

    public List<string> Reasons { get; } = [];

    public Task<string?> VerifyAsync(string reason)
    {
        Reasons.Add(reason);
        return Task.FromResult(Failure);
    }
}
