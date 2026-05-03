namespace Memoa;

/// <summary>
/// Defines a source that can read previously captured HTTP requests.
/// Sinks may optionally implement this interface to support replay scenarios.
/// </summary>
public interface IRequestSource
{
    /// <summary>
    /// Reads captured requests matching the given query.
    /// </summary>
    /// <param name="query">The filter criteria.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async stream of captured requests.</returns>
    IAsyncEnumerable<RecordedRequest> ReadAsync(RequestQuery query, CancellationToken cancellationToken);
}
