namespace Memoa.Internal;

/// <summary>
/// Determines whether a content type is binary based on configured patterns.
/// </summary>
internal sealed class ContentTypeClassifier
{
    private readonly IReadOnlyList<string> _binaryPatterns;

    public ContentTypeClassifier(IReadOnlyList<string> binaryPatterns)
    {
        _binaryPatterns = binaryPatterns;
    }

    /// <summary>
    /// Returns true if the content type matches a binary pattern.
    /// </summary>
    public bool IsBinary(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        // Extract the media type without parameters (e.g., "text/plain; charset=utf-8" → "text/plain")
        var mediaType = contentType;
        var semicolonIndex = contentType.IndexOf(';', StringComparison.Ordinal);
        if (semicolonIndex >= 0)
        {
            mediaType = contentType[..semicolonIndex].Trim();
        }

        return GlobMatcher.IsMatchAny(mediaType, _binaryPatterns);
    }
}
