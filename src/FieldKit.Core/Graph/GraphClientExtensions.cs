namespace FieldKit.Core.Graph;

/// <summary>Typed reads over <see cref="IGraphClient.SendAsync"/> (the extension half of <c>GraphClientProtocol</c>).</summary>
public static class GraphClientExtensions
{
    internal static async Task<T> GetAsync<T>(this IGraphClient client, GraphRequest request, CancellationToken cancellationToken = default)
    {
        var data = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return GraphJson.Decode<T>(data);
    }

    /// <summary>Fetches a collection, following <c>@odata.nextLink</c> up to <paramref name="maxPages"/>.</summary>
    internal static async Task<IReadOnlyList<TItem>> GetAllAsync<TItem>(
        this IGraphClient client,
        GraphRequest request,
        int maxPages = 5,
        CancellationToken cancellationToken = default)
    {
        var items = new List<TItem>();
        GraphRequest? next = request;
        var pages = 0;
        while (next is not null && pages < maxPages)
        {
            var page = await client.GetAsync<GraphCollection<TItem>>(next, cancellationToken).ConfigureAwait(false);
            items.AddRange(page.Value);
            next = page.NextLink is null ? null : GraphRequest.Get(NextLink(page.NextLink), next.Headers);
            pages++;
        }
        return items;
    }

    /// <summary>One page of a collection, for lists that load more as the user scrolls.</summary>
    internal static async Task<GraphPage<TResult>> GetPageAsync<TItem, TResult>(
        this IGraphClient client,
        GraphRequest request,
        Func<TItem, TResult> map,
        CancellationToken cancellationToken = default)
    {
        var page = await client.GetAsync<GraphCollection<TItem>>(request, cancellationToken).ConfigureAwait(false);
        return new GraphPage<TResult>(page.Value.Select(map).ToList(), page.NextLink is null ? null : NextLink(page.NextLink));
    }

    /// <summary>The page after <paramref name="next"/>, keeping the first request's headers.</summary>
    internal static GraphRequest NextPage(Uri next, GraphRequest first) => GraphRequest.Get(NextLink(next.AbsoluteUri), first.Headers);

    /// <summary>
    /// Runs one of Intune's report actions and reads its table. They're reads sent as POST, and
    /// answer with text/plain or octet-stream rather than JSON, so the body is parsed as is.
    /// </summary>
    internal static async Task<ReportTable> PostReportAsync(this IGraphClient client, GraphRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Method != GraphMethod.Post || !request.Url.AbsolutePath.Contains("/deviceManagement/reports/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only Intune report actions are read with POST.", nameof(request));
        }
        return ReportTable.Parse(await client.SendAsync(request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The signed-in tenant's display name. Covered by <c>User.Read</c>.</summary>
    public static async Task<string?> OrganizationNameAsync(this IGraphClient client, CancellationToken cancellationToken = default)
    {
        var request = GraphRequest.Get("organization", [("$select", "id,displayName")]);
        var organizations = await client.GetAllAsync<OrganizationDto>(request, maxPages: 1, cancellationToken).ConfigureAwait(false);
        return organizations.FirstOrDefault()?.DisplayName;
    }

    /// <summary>Next pages are only followed within Graph, so a response can't send the token elsewhere.</summary>
    private static Uri NextLink(string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var url)
            && url.Scheme == Uri.UriSchemeHttps
            && url.Host == GraphRequest.Host)
        {
            return url;
        }
        throw GraphException.Decoding("unreadable @odata.nextLink");
    }
}
