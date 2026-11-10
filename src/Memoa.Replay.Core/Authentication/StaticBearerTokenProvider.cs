using System.Net.Http.Headers;

namespace Memoa.Replay.Authentication;

/// <summary>
/// Authentication provider that applies a static bearer token to every request.
/// </summary>
public sealed class StaticBearerTokenProvider : IReplayAuthenticationProvider
{
    private readonly string _token;

    public StaticBearerTokenProvider(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("The value cannot be null, empty, or whitespace.", nameof(token));
        }
        _token = token;
    }

    public ValueTask AuthenticateAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return ValueTask.CompletedTask;
    }
}
