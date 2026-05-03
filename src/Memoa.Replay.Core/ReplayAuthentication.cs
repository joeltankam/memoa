namespace Memoa.Replay;

/// <summary>
/// Authentication configuration for the replay target.
/// </summary>
public sealed class ReplayAuthentication
{
    /// <summary>
    /// Bearer token to send in the <c>Authorization: Bearer {token}</c> header.
    /// Mutually exclusive with <see cref="HeaderName"/>/<see cref="HeaderValue"/>.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Custom authentication header name (e.g., <c>"Authorization"</c>, <c>"X-Api-Key"</c>).
    /// Used together with <see cref="HeaderValue"/>.
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// Custom authentication header value (e.g., <c>"Basic dXNlcjpwYXNz"</c>, <c>"my-key"</c>).
    /// Used together with <see cref="HeaderName"/>.
    /// </summary>
    public string? HeaderValue { get; set; }

    /// <summary>
    /// Optional callback to customize each outgoing <see cref="HttpRequestMessage"/> before it is sent.
    /// Use this for advanced scenarios like OAuth token refresh, HMAC signing, or mTLS client certs.
    /// </summary>
    public Action<HttpRequestMessage>? ConfigureRequest { get; set; }
}
