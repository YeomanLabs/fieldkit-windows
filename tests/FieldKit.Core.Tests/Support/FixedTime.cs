namespace FieldKit.Core.Tests.Support;

/// <summary>A clock that always reads the same instant.</summary>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
