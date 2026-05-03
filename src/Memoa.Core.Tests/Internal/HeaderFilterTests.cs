using FluentAssertions;
using Memoa.Internal;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(HeaderFilter))]
internal class HeaderFilterTests
{
    [Test]
    public void ShouldInclude_ShouldReturnTrue_WhenNoPatterns()
    {
        // Arrange
        var filter = new HeaderFilter([], []);

        // Act
        var result = filter.ShouldInclude("Content-Type");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ShouldInclude_ShouldExcludeDeniedHeaders()
    {
        // Arrange
        var filter = new HeaderFilter([], ["Authorization", "Cookie"]);

        // Act & Assert
        filter.ShouldInclude("Authorization").Should().BeFalse();
        filter.ShouldInclude("Cookie").Should().BeFalse();
        filter.ShouldInclude("Content-Type").Should().BeTrue();
    }

    [Test]
    public void ShouldInclude_ShouldOnlyIncludeAllowedHeaders()
    {
        // Arrange
        var filter = new HeaderFilter(["Content-Type", "Accept"], []);

        // Act & Assert
        filter.ShouldInclude("Content-Type").Should().BeTrue();
        filter.ShouldInclude("Accept").Should().BeTrue();
        filter.ShouldInclude("Authorization").Should().BeFalse();
    }

    [Test]
    public void ShouldInclude_ShouldDenyOverrideAllow()
    {
        // Arrange — "X-*" allowed, but "X-Secret" denied
        var filter = new HeaderFilter(["X-*"], ["X-Secret"]);

        // Act & Assert
        filter.ShouldInclude("X-Request-Id").Should().BeTrue();
        filter.ShouldInclude("X-Secret").Should().BeFalse();
    }

    [Test]
    public void ShouldInclude_ShouldApplyDefaultCaptureOptions()
    {
        // Arrange
        var options = new MemoaCaptureOptions();
        var filter = new HeaderFilter(options.HeaderAllowList, options.HeaderDenyList);

        // Act & Assert
        filter.ShouldInclude("Content-Type").Should().BeTrue();
        filter.ShouldInclude("Authorization").Should().BeFalse();
        filter.ShouldInclude("Cookie").Should().BeFalse();
        filter.ShouldInclude("Set-Cookie").Should().BeFalse();
        filter.ShouldInclude("Proxy-Authorization").Should().BeFalse();
    }

    [TestCase("authorization")]
    [TestCase("AUTHORIZATION")]
    [TestCase("Authorization")]
    public void ShouldInclude_ShouldBeCaseInsensitive(string headerName)
    {
        // Arrange
        var filter = new HeaderFilter([], ["Authorization"]);

        // Act
        var result = filter.ShouldInclude(headerName);

        // Assert
        result.Should().BeFalse();
    }
}
