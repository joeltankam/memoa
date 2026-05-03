using FluentAssertions;
using Memoa.Sinks.Redis;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace Memoa.Sinks.Redis.Tests;

[TestFixture(TestOf = typeof(RedisSinkExtensions))]
internal class RedisSinkExtensionsTests
{
    [Test]
    public void Redis_WithPreRegisteredMultiplexer_ShouldRegisterSinkAsRequestSink()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var multiplexerMock = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        services.AddSingleton(multiplexerMock.Object);

        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.Redis();

        var sp = services.BuildServiceProvider();
        var sinks = sp.GetServices<IRequestSink>().ToList();

        // Assert
        sinks.Should().ContainSingle();
        sinks[0].Should().BeOfType<RedisSink>();
    }

    [Test]
    public void Redis_WithPreRegisteredMultiplexer_ShouldApplyConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new Mock<IConnectionMultiplexer>(MockBehavior.Strict).Object);

        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.Redis(opts =>
        {
            opts.StreamKey = "custom:stream";
            opts.MaxLength = 500;
            opts.KeyPrefix = "app";
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<RedisSinkOptions>();

        // Assert
        options.StreamKey.Should().Be("custom:stream");
        options.MaxLength.Should().Be(500);
        options.KeyPrefix.Should().Be("app");
    }

    [Test]
    public void Redis_WithPreRegisteredMultiplexer_ShouldReturnSinkBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new Mock<IConnectionMultiplexer>(MockBehavior.Strict).Object);

        var builder = services.AddMemoa();

        // Act
        var result = builder.WriteTo.Redis();

        // Assert
        result.Should().BeSameAs(builder.WriteTo);
    }

    [Test]
    public void Redis_WithPreRegisteredMultiplexer_ShouldUseDefaultOptions_WhenNoConfigure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new Mock<IConnectionMultiplexer>(MockBehavior.Strict).Object);

        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.Redis();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<RedisSinkOptions>();

        // Assert — defaults
        options.StreamKey.Should().Be("memoa:requests");
        options.MaxLength.Should().Be(10_000);
        options.Database.Should().Be(-1);
        options.KeyPrefix.Should().BeNull();
    }
}
