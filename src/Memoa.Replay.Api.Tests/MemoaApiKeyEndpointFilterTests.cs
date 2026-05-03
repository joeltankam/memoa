using System.Net;
using FluentAssertions;
using Memoa.Replay.Api;
using Memoa.Replay.Api.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;

namespace Memoa.Replay.Api.Tests;

/// <summary>
/// Integration tests for <see cref="MemoaApiKeyEndpointFilter"/> via TestServer.
/// </summary>
[TestFixture(TestOf = typeof(MemoaApiKeyEndpointFilter))]
internal class MemoaApiKeyEndpointFilterTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(IHost Host, HttpClient Client)> CreateTestHost(
        Action<MemoaReplayApiOptions>? configureOptions = null)
    {
        var sourceMock = new Mock<IRequestSource>(MockBehavior.Strict);
        sourceMock
            .Setup(s => s.ReadAsync(It.IsAny<RequestQuery>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddSingleton(sourceMock.Object);

                        services.AddMemoaReplay(opts =>
                        {
                            opts.TargetBaseUrl = "http://localhost";
                            configureOptions?.Invoke(opts);
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapMemoaReplay();
                        });
                    });
            })
            .StartAsync();

        return (host, host.GetTestClient());
    }

    private static async IAsyncEnumerable<RecordedRequest> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Endpoint_ShouldReturn200_WhenNoApiKeyConfigured()
    {
        // Arrange
        var (host, client) = await CreateTestHost(); // no ApiKey set

        // Act
        var response = await client.GetAsync("/replay");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Endpoint_ShouldReturn200_WhenCorrectApiKeyProvided()
    {
        // Arrange
        var (host, client) = await CreateTestHost(opts =>
        {
            opts.ApiKey = "my-secret-key";
        });

        client.DefaultRequestHeaders.Add("X-Api-Key", "my-secret-key");

        // Act
        var response = await client.GetAsync("/replay");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Endpoint_ShouldReturn401_WhenApiKeyHeaderMissing()
    {
        // Arrange
        var (host, client) = await CreateTestHost(opts =>
        {
            opts.ApiKey = "my-secret-key";
        });

        // Act — no API key header
        var response = await client.GetAsync("/replay");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Endpoint_ShouldReturn401_WhenWrongApiKeyProvided()
    {
        // Arrange
        var (host, client) = await CreateTestHost(opts =>
        {
            opts.ApiKey = "correct-key";
        });

        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        // Act
        var response = await client.GetAsync("/replay");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Endpoint_ShouldReturn401_WhenWrongCustomHeaderName()
    {
        // Arrange
        var (host, client) = await CreateTestHost(opts =>
        {
            opts.ApiKey = "my-key";
            opts.ApiKeyHeaderName = "X-Custom-Auth";
        });

        // Send in the default header, not the custom one
        client.DefaultRequestHeaders.Add("X-Api-Key", "my-key");

        // Act
        var response = await client.GetAsync("/replay");

        // Assert — should be rejected because the header name is wrong
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Endpoint_ShouldReturn200_WhenCorrectCustomHeaderProvided()
    {
        // Arrange
        var (host, client) = await CreateTestHost(opts =>
        {
            opts.ApiKey = "my-key";
            opts.ApiKeyHeaderName = "X-Custom-Auth";
        });

        client.DefaultRequestHeaders.Add("X-Custom-Auth", "my-key");

        // Act
        var response = await client.GetAsync("/replay");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await host.StopAsync();
        host.Dispose();
    }
}
