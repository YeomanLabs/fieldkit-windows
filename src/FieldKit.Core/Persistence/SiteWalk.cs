namespace FieldKit.Core.Persistence;

/// <summary>One site walk (ports <c>SiteWalkSession</c>). Its scans are stored separately.</summary>
public sealed record SiteWalk(string Id, string Site, string Area, DateTimeOffset StartedAt, DateTimeOffset? EndedAt)
{
    public bool IsActive => EndedAt is null;
}
