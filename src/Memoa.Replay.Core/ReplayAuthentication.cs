using Memoa.Replay.Authentication;

namespace Memoa.Replay;

/// <summary>
/// Authentication configuration for the replay target.
/// When <see cref="Provider"/> is set, it takes precedence over static properties.
/// </summary>
public sealed class ReplayAuthentication
{
    /// <summary>
    /// Dynamic authentication provider that is invoked for each outgoing request.
    /// When set, takes precedence over <see cref="BearerToken"/> and <see cref="HeaderName"/>/<see cref="HeaderValue"/>.
    /// </summary>
    public IReplayAuthenticationProvider? Provider { get; set; }

    /// <summary>
    /// Bearer token to send in the <c>Authorization: Bearer {token}</c> header.
    /// Mutually exclusive with <see cref="HeaderName"/>/<see cref="HeaderValue"/>.
    /// Ignored when <see cref="Provider"/> is set.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Custom authentication header name (e.g., <c>"Authorization"</c>, <c>"X-Api-Key"</c>).
    /// Used together with <see cref="HeaderValue"/>.
    /// Ignored when <see cref="Provider"/> is set.
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// Custom authentication header value (e.g., <c>"Basic dXNlcjpwYXNz"</c>, <c>"my-key"</c>).
    /// Used together with <see cref="HeaderName"/>.
    /// Ignored when <see cref="Provider"/> is set.
    /// </summary>
    public string? HeaderValue { get; set; }

    /// <summary>
    /// Optional callback to customize each outgoing <see cref="HttpRequestMessage"/> before it is sent.
    /// Use this for advanced scenarios like HMAC signing or mTLS client certs.
    /// Invoked after <see cref="Provider"/> or static properties are applied.
    /// </summary>
    public Action<HttpRequestMessage>? ConfigureRequest { get; set; }

    /// <summary>
    /// OAuth client credentials configuration for dynamic token acquisition.
    /// When set and <see cref="Provider"/> is null, an <see cref="OAuthClientCredentialsProvider"/>
    /// is automatically created and used.
    /// </summary>
    public OAuthClientCredentialsOptions? OAuthClientCredentials { get; set; }
}
