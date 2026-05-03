namespace Memoa.Internal;

/// <summary>
/// Filters request paths based on include/exclude glob patterns.
/// </summary>
internal sealed class PathFilter
{
    private readonly IReadOnlyList<string> _includePatterns;
    private readonly IReadOnlyList<string> _excludePatterns;

    public PathFilter(IReadOnlyList<string> includePatterns, IReadOnlyList<string> excludePatterns)
    {
        _includePatterns = includePatterns;
        _excludePatterns = excludePatterns;
    }

    /// <summary>
    /// Returns true if the path should be captured.
    /// </summary>
    public bool ShouldInclude(string path)
    {
        // Must match at least one include pattern
        if (_includePatterns.Count > 0 && !GlobMatcher.IsMatchAny(path, _includePatterns))
        {
            return false;
        }

        // Must not match any exclude pattern
        if (_excludePatterns.Count > 0 && GlobMatcher.IsMatchAny(path, _excludePatterns))
        {
            return false;
        }

        return true;
    }
}
