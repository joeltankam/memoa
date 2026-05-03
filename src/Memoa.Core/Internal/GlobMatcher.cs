using System.Text.RegularExpressions;

namespace Memoa.Internal;

/// <summary>
/// Converts simple glob/wildcard patterns to regex and provides matching.
/// </summary>
internal static class GlobMatcher
{
    /// <summary>
    /// Tests whether a value matches a glob pattern.
    /// Supports '*' (any characters) and '?' (single character).
    /// </summary>
    public static bool IsMatch(string value, string pattern)
    {
        var regexPattern = GlobToRegex(pattern);
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Tests whether a value matches any of the given glob patterns.
    /// </summary>
    public static bool IsMatchAny(string value, IReadOnlyList<string> patterns)
    {
        for (var i = 0; i < patterns.Count; i++)
        {
            if (IsMatch(value, patterns[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string GlobToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        // Replace escaped glob tokens with regex equivalents
        // \*\* matches any path segments
        escaped = escaped.Replace(@"\*\*", ".*");
        // \* matches anything except path separator
        escaped = escaped.Replace(@"\*", "[^/]*");
        // \? matches single character
        escaped = escaped.Replace(@"\?", ".");
        return $"^{escaped}$";
    }
}
