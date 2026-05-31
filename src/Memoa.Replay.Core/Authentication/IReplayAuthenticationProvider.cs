namespace Memoa.Replay.Authentication;

/// <summary>
/// Provides authentication for outgoing replayed HTTP requests.
/// Implement this interface to support custom authentication mechanisms such as
/// OAuth2, HMAC, or any other dynamic token acquisition strategy.
/// </summary>
public interface IReplayAuthenticationProvider
{
    /// <summary>
    /// Applies authentication to the given <see cref="HttpRequestMessage"/> before it is sent.
    /// </summary>
    /// <param name="message">The outgoing HTTP request message to authenticate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    ValueTask AuthenticateAsync(HttpRequestMessage message, CancellationToken cancellationToken);
}
