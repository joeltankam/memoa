namespace Memoa.Internal;

/// <summary>
/// Abstraction over the delivery of captured requests to sinks.
/// </summary>
internal interface IRequestPipeline
{
    /// <summary>
    /// Submits a captured request to be delivered to all registered sinks.
    /// </summary>
    ValueTask SubmitAsync(RecordedRequest request, CancellationToken cancellationToken);
}
