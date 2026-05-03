using FluentAssertions;
using Memoa.Replay;
using Memoa.Replay.Api;
using NUnit.Framework;

namespace Memoa.Replay.Api.Tests;

[TestFixture(TestOf = typeof(MemoaReplayApiOptions))]
internal class MemoaReplayApiOptionsTests
{
    [Test]
    public void Defaults_ShouldHaveExpectedValues()
    {
        // Act
        var options = new MemoaReplayApiOptions();

        // Assert
        options.RoutePrefix.Should().Be("/replay");
        options.AuthorizationPolicy.Should().BeNull();
        options.ApiKeyHeaderName.Should().Be("X-Api-Key");
        options.ApiKey.Should().BeNull();
        options.TargetBaseUrl.Should().BeNull();
        options.DefaultTimelineMode.Should().Be(TimelineMode.None);
        options.MaxParallelism.Should().Be(10);
        options.TargetAuthentication.Should().BeNull();
    }

    [Test]
    public void SectionName_ShouldBe_MemoaReplay()
    {
        MemoaReplayApiOptions.SectionName.Should().Be("Memoa:Replay");
    }

    [Test]
    public void RoutePrefix_CanBeChanged()
    {
        // Arrange
        var options = new MemoaReplayApiOptions();

        // Act
        options.RoutePrefix = "/api/replay";

        // Assert
        options.RoutePrefix.Should().Be("/api/replay");
    }

    [Test]
    public void ApiKey_CanBeSet()
    {
        // Arrange
        var options = new MemoaReplayApiOptions();

        // Act
        options.ApiKey = "secret";

        // Assert
        options.ApiKey.Should().Be("secret");
    }

    [Test]
    public void TargetAuthentication_CanBeSet()
    {
        // Arrange
        var options = new MemoaReplayApiOptions();
        var auth = new ReplayAuthentication { BearerToken = "token" };

        // Act
        options.TargetAuthentication = auth;

        // Assert
        options.TargetAuthentication.Should().BeSameAs(auth);
        options.TargetAuthentication!.BearerToken.Should().Be("token");
    }
}
