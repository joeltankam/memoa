namespace Memoa;

/// <summary>
/// Defines a sink that persists captured HTTP requests.
/// Implementations are responsible for writing to a specific storage backend.
/// </summary>
public interface IRequestSink
{
    /// <summary>
    /// Writes a captured request to the sink.
    /// </summary>
    /// <param name="request">The captured request to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken);
}
