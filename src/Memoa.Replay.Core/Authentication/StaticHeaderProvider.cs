namespace Memoa.Replay.Authentication;

/// <summary>
/// Authentication provider that applies a static header name/value pair to every request.
/// Useful for API key authentication (e.g., <c>X-Api-Key: secret</c>).
/// </summary>
public sealed class StaticHeaderProvider : IReplayAuthenticationProvider
{
    private readonly string _headerName;
    private readonly string _headerValue;

    public StaticHeaderProvider(string headerName, string headerValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerValue);
        _headerName = headerName;
        _headerValue = headerValue;
    }

    public ValueTask AuthenticateAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        message.Headers.TryAddWithoutValidation(_headerName, _headerValue);
        return ValueTask.CompletedTask;
    }
}
