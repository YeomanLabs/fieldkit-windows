using System.Globalization;
using FieldKit.Core.Domain;

namespace FieldKit.Core.Graph;

/// <summary>
/// A person's recent interactive sign-ins, for "why was I blocked?" (Windows only). Needs
/// AuditLog.Read.All; the Conditional Access policies in them also need Policy.Read.ConditionalAccess.
/// </summary>
public sealed class SignInLookup(IGraphClient client, TimeProvider? time = null)
{
    public const int Days = 7;

    public const int Limit = 50;

    // Without this header Graph reports report-only policy results as "unknownFutureValue".
    private static readonly IReadOnlyDictionary<string, string> UnknownEnums =
        new Dictionary<string, string> { ["Prefer"] = "include-unknown-enum-members" };

    /// <summary>Newest first, as Graph returns them. Always filtered by time: unbounded queries time out.</summary>
    public async Task<IReadOnlyList<SignIn>> RecentAsync(string userId, CancellationToken cancellationToken = default)
    {
        var since = (time ?? TimeProvider.System).GetUtcNow().AddDays(-Days).UtcDateTime;
        var request = GraphRequest.Get("auditLogs/signIns",
        [
            ("$filter", $"userId eq {ODataLiteral.String(userId)} and createdDateTime ge {since.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}"),
            ("$top", Limit.ToString(CultureInfo.InvariantCulture)),
        ], UnknownEnums);
        var items = await client.GetAllAsync<SignInDto>(request, maxPages: 1, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.ToDomain()).ToList();
    }
}
