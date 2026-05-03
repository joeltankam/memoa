using FluentAssertions;
using Memoa.Internal;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(PathFilter))]
internal class PathFilterTests
{
    [Test]
    public void ShouldInclude_ShouldReturnTrue_WhenNoPatterns()
    {
        // Arrange
        var filter = new PathFilter([], []);

        // Act
        var result = filter.ShouldInclude("/api/values");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ShouldInclude_ShouldReturnTrue_WhenPathMatchesIncludePattern()
    {
        // Arrange
        var filter = new PathFilter(["/api/**"], []);

        // Act
        var result = filter.ShouldInclude("/api/values");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ShouldInclude_ShouldReturnFalse_WhenPathDoesNotMatchIncludePattern()
    {
        // Arrange
        var filter = new PathFilter(["/api/**"], []);

        // Act
        var result = filter.ShouldInclude("/health");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void ShouldInclude_ShouldReturnFalse_WhenPathMatchesExcludePattern()
    {
        // Arrange
        var filter = new PathFilter(["/**"], ["/health*"]);

        // Act
        var result = filter.ShouldInclude("/healthz");

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void ShouldInclude_ShouldReturnTrue_WhenPathMatchesIncludeButNotExclude()
    {
        // Arrange
        var filter = new PathFilter(["/**"], ["/health*", "/metrics*"]);

        // Act
        var result = filter.ShouldInclude("/api/values");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ShouldInclude_ShouldApplyDefaultFilterOptions()
    {
        // Arrange
        var options = new MemoaFilterOptions();
        var filter = new PathFilter(options.PathIncludePatterns, options.PathExcludePatterns);

        // Act & Assert
        filter.ShouldInclude("/api/values").Should().BeTrue();
        filter.ShouldInclude("/health").Should().BeFalse();
        filter.ShouldInclude("/healthz").Should().BeFalse();
        filter.ShouldInclude("/metrics").Should().BeFalse();
        filter.ShouldInclude("/favicon.ico").Should().BeFalse();
    }
}
