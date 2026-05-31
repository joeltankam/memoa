namespace Memoa.Replay.Authentication;

/// <summary>
/// Configuration options for OAuth 2.0 client credentials token acquisition.
/// </summary>
public sealed class OAuthClientCredentialsOptions
{
    /// <summary>
    /// The token endpoint URL (e.g., <c>https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token</c>).
    /// </summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth client identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The requested scope (e.g., <c>api://my-app/.default</c>).
    /// When <c>null</c>, no scope is sent in the token request.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// The requested resource (for Azure AD v1 endpoints).
    /// When <c>null</c>, no resource is sent in the token request.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Buffer time before token expiry to trigger a refresh.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan ExpiryBuffer { get; set; } = TimeSpan.FromSeconds(60);
}
