using System.Net.Http.Headers;
using System.Text.Json;

namespace Memoa.Replay.Authentication;

/// <summary>
/// Authentication provider that acquires OAuth 2.0 access tokens using the client credentials grant.
/// Tokens are cached in memory and refreshed automatically before expiry.
/// </summary>
public sealed class OAuthClientCredentialsProvider : IReplayAuthenticationProvider, IDisposable
{
    private readonly OAuthClientCredentialsOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public OAuthClientCredentialsProvider(OAuthClientCredentialsOptions options)
        : this(options, new HttpClient(), ownsHttpClient: true)
    {
    }

    public OAuthClientCredentialsProvider(OAuthClientCredentialsOptions options, HttpClient httpClient)
        : this(options, httpClient, ownsHttpClient: false)
    {
    }

    private OAuthClientCredentialsProvider(OAuthClientCredentialsOptions options, HttpClient httpClient, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(options.TokenEndpoint))
        {
            throw new ArgumentException("TokenEndpoint is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ArgumentException("ClientId is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new ArgumentException("ClientSecret is required.", nameof(options));
        }

        _options = options;
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public async ValueTask AuthenticateAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            return await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", _options.ClientId),
            new("client_secret", _options.ClientSecret)
        };

        if (!string.IsNullOrEmpty(_options.Scope))
        {
            parameters.Add(new("scope", _options.Scope));
        }

        if (!string.IsNullOrEmpty(_options.Resource))
        {
            parameters.Add(new("resource", _options.Resource));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var accessToken = document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response did not contain an access_token.");

        var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresInElement)
            ? TimeSpan.FromSeconds(expiresInElement.GetInt32())
            : TimeSpan.FromHours(1);

        _cachedToken = accessToken;
        _tokenExpiresAt = DateTimeOffset.UtcNow + expiresIn - _options.ExpiryBuffer;

        return accessToken;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
