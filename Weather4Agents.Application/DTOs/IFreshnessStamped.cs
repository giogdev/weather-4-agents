namespace Weather4Agents.Application.DTOs;

/// <summary>
/// Marks a forecast response as carrying the moment its data was scraped. The API layer uses this
/// to derive an <c>ETag</c> (see the conditional-request handling on the weather controller), so a
/// polling agent can revalidate with <c>If-None-Match</c> and get <c>304 Not Modified</c> whenever
/// the underlying data has not been re-scraped.
/// </summary>
public interface IFreshnessStamped
{
    /// <summary>
    /// When the underlying data was scraped from the provider (UTC), not when the response was
    /// produced. A response served from cache reports the original scrape time.
    /// </summary>
    DateTimeOffset LastUpdatedAt { get; }
}
