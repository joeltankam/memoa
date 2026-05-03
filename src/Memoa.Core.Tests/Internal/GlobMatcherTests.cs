using FluentAssertions;
using Memoa.Internal;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(GlobMatcher))]
internal class GlobMatcherTests
{
    [TestCase("/api/values", "/api/values", true)]
    [TestCase("/api/values", "/api/other", false)]
    [TestCase("/api/values", "/api/*", true)]
    [TestCase("/api/values/123", "/api/*/123", true)]
    [TestCase("/api/values/123", "/api/*", false)]
    [TestCase("/health", "/health*", true)]
    [TestCase("/healthz", "/health*", true)]
    [TestCase("/health/ready", "/health*", false)]
    [TestCase("/a/b/c/d", "/**", true)]
    [TestCase("/api/v1/users/42", "/api/**/42", true)]
    [TestCase("/favicon.ico", "/favicon.ico", true)]
    [TestCase("/Favicon.ICO", "/favicon.ico", true)]
    [TestCase("/ab", "/a?", true)]
    [TestCase("/abc", "/a?", false)]
    public void IsMatch_ShouldReturnExpected(string value, string pattern, bool expected)
    {
        // Act
        var result = GlobMatcher.IsMatch(value, pattern);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void IsMatchAny_ShouldReturnTrue_WhenAnyPatternMatches()
    {
        // Arrange
        var patterns = new List<string> { "/health*", "/metrics*", "/favicon.ico" };

        // Act & Assert
        GlobMatcher.IsMatchAny("/healthz", patterns).Should().BeTrue();
        GlobMatcher.IsMatchAny("/metrics", patterns).Should().BeTrue();
        GlobMatcher.IsMatchAny("/favicon.ico", patterns).Should().BeTrue();
    }

    [Test]
    public void IsMatchAny_ShouldReturnFalse_WhenNoPatternMatches()
    {
        // Arrange
        var patterns = new List<string> { "/health*", "/metrics*" };

        // Act
        var result = GlobMatcher.IsMatchAny("/api/values", patterns);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsMatchAny_ShouldReturnFalse_WhenPatternsEmpty()
    {
        // Arrange
        var patterns = new List<string>();

        // Act
        var result = GlobMatcher.IsMatchAny("/anything", patterns);

        // Assert
        result.Should().BeFalse();
    }
}
