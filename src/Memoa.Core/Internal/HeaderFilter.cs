namespace Memoa.Internal;

/// <summary>
/// Filters HTTP headers based on allow/deny glob patterns.
/// </summary>
internal sealed class HeaderFilter
{
    private readonly IReadOnlyList<string> _allowPatterns;
    private readonly IReadOnlyList<string> _denyPatterns;

    public HeaderFilter(IReadOnlyList<string> allowPatterns, IReadOnlyList<string> denyPatterns)
    {
        _allowPatterns = allowPatterns;
        _denyPatterns = denyPatterns;
    }

    /// <summary>
    /// Returns true if the header name should be included in the capture.
    /// </summary>
    public bool ShouldInclude(string headerName)
    {
        // If deny list matches, always exclude
        if (_denyPatterns.Count > 0 && GlobMatcher.IsMatchAny(headerName, _denyPatterns))
        {
            return false;
        }

        // If allow list is empty, include everything (minus denied)
        if (_allowPatterns.Count == 0)
        {
            return true;
        }

        // Otherwise, include only if allow list matches
        return GlobMatcher.IsMatchAny(headerName, _allowPatterns);
    }
}
