namespace Memoa.Replay.Authentication;

/// <summary>
/// Chains multiple <see cref="IReplayAuthenticationProvider"/> instances, applying them in order.
/// Useful for combining authentication mechanisms (e.g., OAuth + custom tracing header).
/// </summary>
public sealed class CompositeAuthenticationProvider : IReplayAuthenticationProvider
{
    private readonly IReplayAuthenticationProvider[] _providers;

    public CompositeAuthenticationProvider(params IReplayAuthenticationProvider[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers;
    }

    public async ValueTask AuthenticateAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        foreach (var provider in _providers)
        {
            await provider.AuthenticateAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
