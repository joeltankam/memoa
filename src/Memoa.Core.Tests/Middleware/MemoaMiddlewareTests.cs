using System.Net;
using System.Text;
using FluentAssertions;
using Memoa.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(MemoaMiddleware))]
internal class MemoaMiddlewareTests
{
    private static async Task<(IHost Host, HttpClient Client, Mock<IRequestSink> Sink)> CreateTestHost(
        Action<MemoaOptions>? configureOptions = null,
        RequestDelegate? handler = null)
    {
        var sinkMock = new Mock<IRequestSink>(MockBehavior.Strict);
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddMemoa(opts =>
                        {
                            opts.Pipeline.Mode = PipelineMode.Inline;
                            configureOptions?.Invoke(opts);
                        });
                        services.AddSingleton<IRequestSink>(sinkMock.Object);
                    })
                    .Configure(app =>
                    {
                        app.UseMemoa();
                        app.Run(handler ?? (static async ctx => await ctx.Response.WriteAsync("OK")));
                    });
            })
            .StartAsync();

        return (host, host.GetTestClient(), sinkMock);
    }

    [Test]
    public async Task Middleware_ShouldCaptureGetRequest()
    {
        // Arrange
        var (host, client, sinkMock) = await CreateTestHost();
        RecordedRequest? captured = null;
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        // Act
        var response = await client.GetAsync("/api/test?key=value");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        captured.Should().NotBeNull();
        captured!.Method.Should().Be("GET");
        captured.Path.Should().Be("/api/test");
        captured.QueryString.Should().Be("?key=value");

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldCapturePostBodyAsText()
    {
        // Arrange
        RecordedRequest? captured = null;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Capture.IncludeBody = true;
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        var content = new StringContent("{\"name\":\"test\"}", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        captured.Should().NotBeNull();
        captured!.Method.Should().Be("POST");
        captured.Body.Should().NotBeNull();
        captured.Body!.Text.Should().Be("{\"name\":\"test\"}");
        captured.Body.ContentType.Should().Contain("application/json");
        captured.Body.Truncated.Should().BeFalse();

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldSkipRequest_WhenDisabled()
    {
        // Arrange
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Enabled = false;
        });

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sinkMock.Verify(
            s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldSkipRequest_WhenPathExcluded()
    {
        // Arrange
        var (host, client, sinkMock) = await CreateTestHost();

        // Act — /health is excluded by default
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sinkMock.Verify(
            s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldSkipRequest_WhenMethodNotInFilter()
    {
        // Arrange
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Filters.Methods = ["POST"];
        });

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sinkMock.Verify(
            s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldCaptureHeaders()
    {
        // Arrange
        RecordedRequest? captured = null;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Capture.IncludeHeaders = true;
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        client.DefaultRequestHeaders.Add("X-Custom-Header", "custom-value");

        // Act
        await client.GetAsync("/api/test");

        // Assert
        captured.Should().NotBeNull();
        captured!.Headers.Should().NotBeNull();
        captured.Headers!.Should().ContainKey("X-Custom-Header");
        captured.Headers["X-Custom-Header"].Should().Contain("custom-value");

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldFilterOutDeniedHeaders()
    {
        // Arrange
        RecordedRequest? captured = null;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Capture.IncludeHeaders = true;
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        client.DefaultRequestHeaders.Add("Authorization", "Bearer secret");

        // Act
        await client.GetAsync("/api/test");

        // Assert
        captured.Should().NotBeNull();
        captured!.Headers.Should().NotBeNull();
        captured.Headers!.Should().NotContainKey("Authorization");

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldCaptureCorrelationId()
    {
        // Arrange
        RecordedRequest? captured = null;
        var (host, client, sinkMock) = await CreateTestHost();
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        client.DefaultRequestHeaders.Add("X-Correlation-Id", "abc-123");

        // Act
        await client.GetAsync("/api/test");

        // Assert
        captured.Should().NotBeNull();
        captured!.CorrelationId.Should().Be("abc-123");

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldCaptureResponse_WhenEnabled()
    {
        // Arrange
        RecordedRequest? captured = null;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Capture.IncludeResponse = true;
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("OK");

        captured.Should().NotBeNull();
        captured!.Response.Should().NotBeNull();
        captured.Response!.StatusCode.Should().Be(200);
        captured.Response.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldNotCaptureQueryString_WhenDisabled()
    {
        // Arrange
        RecordedRequest? captured = null;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Capture.IncludeQueryString = false;
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        // Act
        await client.GetAsync("/api/test?key=value");

        // Assert
        captured.Should().NotBeNull();
        captured!.QueryString.Should().BeNull();

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldNotCaptureHeaders_WhenDisabled()
    {
        // Arrange
        RecordedRequest? captured = null;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Capture.IncludeHeaders = false;
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => captured = r)
            .Returns(ValueTask.CompletedTask);

        // Act
        await client.GetAsync("/api/test");

        // Assert
        captured.Should().NotBeNull();
        captured!.Headers.Should().BeNull();

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldCaptureAllMethods_WhenFilterEmpty()
    {
        // Arrange
        var callCount = 0;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Filters.Methods = [];
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((_, _) => Interlocked.Increment(ref callCount))
            .Returns(ValueTask.CompletedTask);

        // Act
        await client.GetAsync("/api/test");
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/test"));

        // Assert — both captured since filter is empty (all methods)
        callCount.Should().Be(2);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldPassRequestThroughToDownstream()
    {
        // Arrange
        var downstreamCalled = false;
        var (host, client, _) = await CreateTestHost(handler: async ctx =>
        {
            downstreamCalled = true;
            await ctx.Response.WriteAsync("downstream");
        });

        // Act
        var response = await client.GetAsync("/api/test");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        downstreamCalled.Should().BeTrue();
        body.Should().Be("downstream");

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldSkipAllRequests_WhenSamplingRateIsZero()
    {
        // Arrange
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Sampling.Rate = 0.0;
        });

        // Act
        for (var i = 0; i < 10; i++)
        {
            await client.GetAsync("/api/test");
        }

        // Assert — nothing should be captured when rate is 0
        sinkMock.Verify(
            s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await host.StopAsync();
        host.Dispose();
    }

    [Test]
    public async Task Middleware_ShouldCaptureAllRequests_WhenSamplingRateIsFull()
    {
        // Arrange
        var callCount = 0;
        var (host, client, sinkMock) = await CreateTestHost(opts =>
        {
            opts.Sampling.Rate = 1.0;
        });
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((_, _) => Interlocked.Increment(ref callCount))
            .Returns(ValueTask.CompletedTask);

        // Act
        for (var i = 0; i < 5; i++)
        {
            await client.GetAsync("/api/test");
        }

        // Assert — all 5 requests captured when rate is 1.0
        callCount.Should().Be(5);

        await host.StopAsync();
        host.Dispose();
    }
}
