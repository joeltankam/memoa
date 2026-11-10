using System.Net;
using FluentAssertions;
using Memoa;
using Memoa.Replay.Api.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;

namespace Memoa.Replay.Api.Tests;

/// <summary>
/// Integration tests for the replay list endpoint mapped by <see cref="MemoaReplayEndpointRouteBuilderExtensions"/>.
/// </summary>
[TestFixture(TestOf = typeof(MemoaReplayEndpointRouteBuilderExtensions))]
internal class MemoaReplayEndpointRouteBuilderExtensionsTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(IHost Host, HttpClient Client, Mock<IRequestSource> SourceMock)> CreateTestHost()
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

        return (host, host.GetTestClient(), sourceMock);
    }

    private static async IAsyncEnumerable<RecordedRequest> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [TestCase("from")]
    [TestCase("to")]
    public async Task ListRequests_ShouldReturn400_WhenDateQueryParameterIsMalformed(string parameter)
    {
        // Arrange
        var (host, client, sourceMock) = await CreateTestHost();

        // Act
        var response = await client.GetAsync($"/replay?{parameter}=not-a-date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(parameter);

        sourceMock.Verify(s => s.ReadAsync(It.IsAny<RequestQuery>(), It.IsAny<CancellationToken>()), Times.Never);

        await host.StopAsync();
        host.Dispose();
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("1.5")]
    [TestCase("99999999999999999999")]
    public async Task ListRequests_ShouldReturn400_WhenTakeQueryParameterIsMalformed(string value)
    {
        // Arrange
        var (host, client, sourceMock) = await CreateTestHost();

        // Act
        var response = await client.GetAsync($"/replay?take={value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("take");

        sourceMock.Verify(s => s.ReadAsync(It.IsAny<RequestQuery>(), It.IsAny<CancellationToken>()), Times.Never);

        await host.StopAsync();
        host.Dispose();
    }

    [TestCase("")]
    public async Task ListRequests_ShouldReturn400_WhenDateQueryParameterIsEmpty(string value)
    {
        // Arrange
        var (host, client, sourceMock) = await CreateTestHost();

        // Act
        var response = await client.GetAsync($"/replay?from={value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        sourceMock.Verify(s => s.ReadAsync(It.IsAny<RequestQuery>(), It.IsAny<CancellationToken>()), Times.Never);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task ListRequests_ShouldReturn200AndForwardQuery_WhenQueryParametersAreValid()
    {
        // Arrange
        RequestQuery? capturedQuery = null;
        var sourceMock = new Mock<IRequestSource>(MockBehavior.Strict);
        sourceMock
            .Setup(s => s.ReadAsync(It.IsAny<RequestQuery>(), It.IsAny<CancellationToken>()))
            .Callback<RequestQuery, CancellationToken>((query, _) => capturedQuery = query)
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
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/replay?from=2026-05-01T00:00:00Z&to=2026-05-02T00:00:00Z&take=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.From.Should().Be(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        capturedQuery.To.Should().Be(new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero));
        capturedQuery.Take.Should().Be(5);

        sourceMock.VerifyAll();

        await host.StopAsync();
        host.Dispose();
    }
}
