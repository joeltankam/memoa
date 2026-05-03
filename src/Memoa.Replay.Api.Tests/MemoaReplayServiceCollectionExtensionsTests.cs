using FluentAssertions;
using Memoa.Replay.Api;
using Memoa.Replay.Api.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Memoa.Replay.Api.Tests;

[TestFixture(TestOf = typeof(MemoaReplayServiceCollectionExtensions))]
internal class MemoaReplayServiceCollectionExtensionsTests
{
    [Test]
    public void AddMemoaReplay_ShouldRegisterOptions_WithDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRequestSource>(new DummyRequestSource());

        // Act
        services.AddMemoaReplay();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<MemoaReplayApiOptions>();

        // Assert
        options.Should().NotBeNull();
        options.RoutePrefix.Should().Be("/replay");
        options.ApiKeyHeaderName.Should().Be("X-Api-Key");
        options.MaxParallelism.Should().Be(10);
    }

    [Test]
    public void AddMemoaReplay_ShouldApplyConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMemoaReplay(opts =>
        {
            opts.RoutePrefix = "/api/replay";
            opts.ApiKey = "secret-key";
            opts.MaxParallelism = 5;
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<MemoaReplayApiOptions>();

        // Assert
        options.RoutePrefix.Should().Be("/api/replay");
        options.ApiKey.Should().Be("secret-key");
        options.MaxParallelism.Should().Be(5);
    }

    [Test]
    public void AddMemoaReplay_ShouldRegisterReplayJobTracker()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRequestSource>(new DummyRequestSource());

        // Act
        services.AddMemoaReplay();

        var sp = services.BuildServiceProvider();
        var tracker = sp.GetService<ReplayJobTracker>();

        // Assert
        tracker.Should().NotBeNull();
    }

    [Test]
    public void AddMemoaReplay_WithConfiguration_ShouldBindOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configData = new Dictionary<string, string?>
        {
            ["RoutePrefix"] = "/my-replay",
            ["ApiKey"] = "config-key",
            ["MaxParallelism"] = "3"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        services.AddMemoaReplay(configuration);

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<MemoaReplayApiOptions>();

        // Assert
        options.RoutePrefix.Should().Be("/my-replay");
        options.ApiKey.Should().Be("config-key");
        options.MaxParallelism.Should().Be(3);
    }

    [Test]
    public void AddMemoaReplay_ShouldReturnServiceCollection_ForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var result = services.AddMemoaReplay();

        // Assert
        result.Should().BeSameAs(services);
    }
}

internal sealed class DummyRequestSource : IRequestSource
{
    public async IAsyncEnumerable<RecordedRequest> ReadAsync(
        RequestQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
