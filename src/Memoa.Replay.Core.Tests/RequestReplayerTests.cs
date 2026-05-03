using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Memoa.Replay.Tests;

[TestFixture(TestOf = typeof(RequestReplayer))]
internal class RequestReplayerTests
{
    // ── Fake HTTP handler ─────────────────────────────────────────────────────

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly Exception? _exception;

        public List<HttpRequestMessage> CapturedRequests { get; } = [];
        /// <summary>Bodies captured before the message is disposed, keyed by request index.</summary>
        public List<byte[]?> CapturedBodies { get; } = [];

        public FakeHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, Exception? exception = null)
        {
            _statusCode = statusCode;
            _exception = exception;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Eagerly read content before the caller disposes the message
            byte[]? body = request.Content is not null
                ? await request.Content.ReadAsByteArrayAsync(cancellationToken)
                : null;

            CapturedRequests.Add(request);
            CapturedBodies.Add(body);

            if (_exception is not null) throw _exception;
            return new HttpResponseMessage(_statusCode);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RecordedRequest CreateRequest(
        string method = "GET",
        string path = "/api/test",
        string? queryString = null,
        DateTimeOffset? capturedAt = null,
        RecordedBody? body = null,
        IReadOnlyDictionary<string, string[]>? headers = null) => new()
    {
        Id = Guid.NewGuid(),
        CapturedAtUtc = capturedAt ?? DateTimeOffset.UtcNow,
        Method = method,
        Scheme = "http",
        Host = "localhost",
        Path = path,
        QueryString = queryString,
        Protocol = "HTTP/1.1",
        Body = body,
        Headers = headers
    };

    private static async IAsyncEnumerable<RecordedRequest> ToAsyncEnumerable(
        IEnumerable<RecordedRequest> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private static RequestReplayer CreateReplayer(
        FakeHttpMessageHandler handler,
        ReplayOptions? options = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new RequestReplayer(
            httpClient,
            options ?? new ReplayOptions(),
            NullLogger<RequestReplayer>.Instance);
    }

    // ── Empty source ──────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldReturnZero_WhenSourceIsEmpty()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);

        // Act
        var result = await sut.ReplayAsync(
            ToAsyncEnumerable([]),
            onOutcome: null,
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        result.Succeeded.Should().Be(0);
        result.Failed.Should().Be(0);
        handler.CapturedRequests.Should().BeEmpty();
    }

    // ── Dry run ───────────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldSucceedWithoutHttpCalls_WhenDryRun()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler, new ReplayOptions { DryRun = true });
        var requests = new[] { CreateRequest(), CreateRequest("POST", "/api/orders") };

        // Act
        var result = await sut.ReplayAsync(
            ToAsyncEnumerable(requests),
            onOutcome: null,
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);
        handler.CapturedRequests.Should().BeEmpty();
    }

    // ── Success and failure counting ──────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldCountSucceeded_WhenHttpResponseReceived()
    {
        // Arrange — any HTTP response (even 4xx/5xx) is a success (got a response)
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var sut = CreateReplayer(handler);

        // Act
        var result = await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest()]),
            onOutcome: null,
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(0);
    }

    [Test]
    public async Task ReplayAsync_ShouldCountFailed_WhenHttpThrows()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(exception: new HttpRequestException("connection refused"));
        var sut = CreateReplayer(handler);

        // Act
        var result = await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest()]),
            onOutcome: null,
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Succeeded.Should().Be(0);
        result.Failed.Should().Be(1);
    }

    [Test]
    public async Task ReplayAsync_ShouldAccumulateCounts_ForMixedResults()
    {
        // Arrange — first request succeeds, second throws
        var callCount = 0;
        var mixedHandler = new MixedResultHandler(
            () => ++callCount == 1
                ? (HttpStatusCode.OK, null)
                : (HttpStatusCode.OK, new HttpRequestException("fail")));
        var httpClient = new HttpClient(mixedHandler) { BaseAddress = new Uri("http://localhost") };
        var sut = new RequestReplayer(httpClient, new ReplayOptions(), NullLogger<RequestReplayer>.Instance);

        // Act
        var result = await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest(), CreateRequest()]),
            onOutcome: null,
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
    }

    private sealed class MixedResultHandler(Func<(HttpStatusCode Status, Exception? Exception)> factory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, ex) = factory();
            if (ex is not null) throw ex;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    // ── Replay header ─────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldAddReplayHeader_ToEveryRequest()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);
        var requests = new[] { CreateRequest(), CreateRequest("POST", "/api/orders") };

        // Act
        await sut.ReplayAsync(ToAsyncEnumerable(requests), null, CancellationToken.None);

        // Assert
        handler.CapturedRequests.Should().HaveCount(2);
        foreach (var msg in handler.CapturedRequests)
        {
            msg.Headers.TryGetValues(RequestReplayer.ReplayHeader, out var vals).Should().BeTrue();
            vals.Should().Contain("true");
        }
    }

    // ── Header forwarding ─────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldForwardCustomHeaders()
    {
        // Arrange
        var headers = new Dictionary<string, string[]>
        {
            ["X-Custom-Header"] = ["custom-value"],
            ["Accept"] = ["application/json"]
        };
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest(headers: headers)]),
            null,
            CancellationToken.None);

        // Assert
        var msg = handler.CapturedRequests.Single();
        msg.Headers.TryGetValues("X-Custom-Header", out var custom).Should().BeTrue();
        custom.Should().Contain("custom-value");
        msg.Headers.TryGetValues("Accept", out var accept).Should().BeTrue();
        accept.Should().Contain("application/json");
    }

    // Host and Transfer-Encoding live on request headers; Content-* live on content headers
    // so we test only the request-header-level blocks here.
    [TestCase("Host")]
    [TestCase("Transfer-Encoding")]
    public async Task ReplayAsync_ShouldNotForwardBlockedHeader(string blockedHeader)
    {
        // Arrange
        var headers = new Dictionary<string, string[]>
        {
            [blockedHeader] = ["some-value"],
            ["X-Keep"] = ["keep-value"]
        };
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest(headers: headers)]),
            null,
            CancellationToken.None);

        // Assert
        var msg = handler.CapturedRequests.Single();
        msg.Headers.Contains(blockedHeader).Should().BeFalse(
            because: $"{blockedHeader} should be stripped from replayed requests");
    }

    // ── Body forwarding ───────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldForwardTextBody()
    {
        // Arrange
        var body = new RecordedBody { Text = "{\"key\":\"value\"}", ContentType = "application/json", Length = 15, Truncated = false };
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest(method: "POST", body: body)]),
            null,
            CancellationToken.None);

        // Assert — body was captured inside the handler before message disposal
        handler.CapturedBodies.Should().HaveCount(1);
        var text = System.Text.Encoding.UTF8.GetString(handler.CapturedBodies[0]!);
        text.Should().Contain("{\"key\":\"value\"}");
    }

    [Test]
    public async Task ReplayAsync_ShouldForwardBinaryBody()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3, 4 };
        var body = new RecordedBody { Base64Bytes = Convert.ToBase64String(bytes), ContentType = "application/octet-stream", Length = 4, Truncated = false };
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest(method: "POST", body: body)]),
            null,
            CancellationToken.None);

        // Assert
        handler.CapturedBodies.Should().HaveCount(1);
        handler.CapturedBodies[0].Should().Equal(bytes);
    }

    [Test]
    public async Task ReplayAsync_ShouldNotSetContent_WhenNoBody()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest(method: "GET", body: null)]),
            null,
            CancellationToken.None);

        // Assert
        handler.CapturedBodies.Single().Should().BeNull();
    }

    // ── Authentication ────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldApplyBearerToken_WhenAuthenticationConfigured()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new ReplayOptions
        {
            Authentication = new ReplayAuthentication { BearerToken = "my-secret-token" }
        };
        var sut = CreateReplayer(handler, options);

        // Act
        await sut.ReplayAsync(ToAsyncEnumerable([CreateRequest()]), null, CancellationToken.None);

        // Assert
        var msg = handler.CapturedRequests.Single();
        msg.Headers.Authorization.Should().NotBeNull();
        msg.Headers.Authorization!.Scheme.Should().Be("Bearer");
        msg.Headers.Authorization!.Parameter.Should().Be("my-secret-token");
    }

    [Test]
    public async Task ReplayAsync_ShouldApplyCustomHeader_WhenAuthenticationConfigured()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new ReplayOptions
        {
            Authentication = new ReplayAuthentication { HeaderName = "X-Api-Key", HeaderValue = "key-value" }
        };
        var sut = CreateReplayer(handler, options);

        // Act
        await sut.ReplayAsync(ToAsyncEnumerable([CreateRequest()]), null, CancellationToken.None);

        // Assert
        var msg = handler.CapturedRequests.Single();
        msg.Headers.TryGetValues("X-Api-Key", out var values).Should().BeTrue();
        values.Should().Contain("key-value");
    }

    [Test]
    public async Task ReplayAsync_ShouldInvokeConfigureRequest_WhenAuthenticationConfigured()
    {
        // Arrange
        var configureInvoked = false;
        var handler = new FakeHttpMessageHandler();
        var options = new ReplayOptions
        {
            Authentication = new ReplayAuthentication
            {
                ConfigureRequest = _ => configureInvoked = true
            }
        };
        var sut = CreateReplayer(handler, options);

        // Act
        await sut.ReplayAsync(ToAsyncEnumerable([CreateRequest()]), null, CancellationToken.None);

        // Assert
        configureInvoked.Should().BeTrue();
    }

    [Test]
    public async Task ReplayAsync_ShouldNotSetAuthHeader_WhenNoAuthentication()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler, new ReplayOptions { Authentication = null });

        // Act
        await sut.ReplayAsync(ToAsyncEnumerable([CreateRequest()]), null, CancellationToken.None);

        // Assert
        handler.CapturedRequests.Single().Headers.Authorization.Should().BeNull();
    }

    // ── Outcome callback ──────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldInvokeOnOutcome_ForEachRequest()
    {
        // Arrange
        var outcomes = new List<ReplayOutcome>();
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);
        var requests = new[] { CreateRequest(), CreateRequest("POST", "/api/orders") };

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable(requests),
            onOutcome: o => outcomes.Add(o),
            CancellationToken.None);

        // Assert
        outcomes.Should().HaveCount(2);
        outcomes.Should().AllSatisfy(o => o.Success.Should().BeTrue());
    }

    [Test]
    public async Task ReplayAsync_DryRun_ShouldInvokeOnOutcome_WithNoStatusCode()
    {
        // Arrange
        var outcomes = new List<ReplayOutcome>();
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler, new ReplayOptions { DryRun = true });

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest()]),
            onOutcome: o => outcomes.Add(o),
            CancellationToken.None);

        // Assert
        outcomes.Should().HaveCount(1);
        outcomes[0].StatusCode.Should().BeNull();
        outcomes[0].Success.Should().BeTrue();
    }

    // ── Sorting ───────────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldSortRequestsByTimestamp_BeforeSending()
    {
        // Arrange
        var t0 = DateTimeOffset.UtcNow;
        var req1 = CreateRequest(path: "/first", capturedAt: t0.AddSeconds(2));
        var req2 = CreateRequest(path: "/second", capturedAt: t0);
        var req3 = CreateRequest(path: "/third", capturedAt: t0.AddSeconds(1));
        var sentPaths = new List<string>();

        var handler = new CapturingPathHandler(sentPaths);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var sut = new RequestReplayer(httpClient, new ReplayOptions(), NullLogger<RequestReplayer>.Instance);

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([req1, req2, req3]),
            onOutcome: null,
            CancellationToken.None);

        // Assert — sent in chronological order (req2 → req3 → req1)
        sentPaths.Should().Equal("/second", "/third", "/first");
    }

    private sealed class CapturingPathHandler(List<string> paths) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            paths.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    // ── Parallel mode ─────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldSendAllRequests_InParallelMode()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler, new ReplayOptions { Mode = TimelineMode.None, Parallelism = 3 });
        var requests = Enumerable.Range(0, 5).Select(i => CreateRequest(path: $"/api/item/{i}")).ToArray();

        // Act
        var result = await sut.ReplayAsync(ToAsyncEnumerable(requests), null, CancellationToken.None);

        // Assert
        result.Total.Should().Be(5);
        result.Succeeded.Should().Be(5);
        handler.CapturedRequests.Should().HaveCount(5);
    }

    // ── Relative timeline mode ────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldSendAllRequests_InRelativeMode()
    {
        // Arrange — all at same timestamp so no waiting
        var now = DateTimeOffset.UtcNow;
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler, new ReplayOptions { Mode = TimelineMode.Relative });
        var requests = Enumerable.Range(0, 3)
            .Select(i => CreateRequest(path: $"/api/{i}", capturedAt: now))
            .ToArray();

        // Act
        var result = await sut.ReplayAsync(ToAsyncEnumerable(requests), null, CancellationToken.None);

        // Assert
        result.Total.Should().Be(3);
        result.Succeeded.Should().Be(3);
        handler.CapturedRequests.Should().HaveCount(3);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldThrow_WhenCancelledBeforeStart()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest()]),
            null,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Query string ──────────────────────────────────────────────────────────

    [Test]
    public async Task ReplayAsync_ShouldAppendQueryString_WhenPresent()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var sut = CreateReplayer(handler);

        // Act
        await sut.ReplayAsync(
            ToAsyncEnumerable([CreateRequest(queryString: "?foo=bar&baz=1")]),
            null,
            CancellationToken.None);

        // Assert
        handler.CapturedRequests.Single().RequestUri!.Query.Should().Be("?foo=bar&baz=1");
    }
}
