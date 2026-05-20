namespace Bcs.InvestApi.Infrastructure;

internal static class BcsUri
{
    public static Uri EnsureTrailingSlash(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            return uri;
        }

        return new Uri(uri.AbsoluteUri + "/");
    }
}
