using FieldKit.Core.Services;

namespace FieldKit.Core.Tests.Support;

internal sealed class FakeSecureClipboard : ISecureClipboard
{
    public List<string> Copied { get; } = [];

    public void Copy(string secret) => Copied.Add(secret);
}
