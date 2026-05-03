namespace Memoa;

/// <summary>
/// Configures request sampling for the Memoa middleware.
/// </summary>
public sealed class MemoaSamplingOptions
{
    /// <summary>
    /// Fraction of requests to capture. Must be between <c>0.0</c> (capture none)
    /// and <c>1.0</c> (capture all). Default: <c>1.0</c>.
    /// </summary>
    public double Rate { get; set; } = 1.0;
}
