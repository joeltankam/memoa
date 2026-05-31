using FluentAssertions;
using Memoa.Replay.Api.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using System.Net;

namespace Memoa.Replay.Api.Tests.Internal;

[TestFixture(TestOf = typeof(ReplayJobTracker))]
internal class ReplayJobTrackerTests
{
    // ── Fake HTTP handler ─────────────────────────────────────────────────────

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> CapturedRequests { get; } = [];
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            return Task.FromResult(new HttpResponseMessage(StatusCode));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RecordedRequest CreateRecordedRequest(string method = "GET", string path = "/api/test") => new()
    {
        Id = Guid.NewGuid(),
        CapturedAtUtc = DateTimeOffset.UtcNow,
        Method = method,
        Scheme = "http",
        Host = "localhost",
        Path = path,
        Protocol = "HTTP/1.1"
    };

    private static async IAsyncEnumerable<RecordedRequest> ToAsyncEnumerable(
        IEnumerable<RecordedRequest> items)
    {
        foreach (var item in items) yield return item;
        await Task.CompletedTask;
    }

    private static (ReplayJobTracker Tracker, FakeHttpMessageHandler Handler) CreateTracker(
        MemoaReplayApiOptions? options = null,
        IEnumerable<RecordedRequest>? requests = null)
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.Is<string>(n => n == "MemoaReplay")))
            .Returns(httpClient);

        var requestSourceMock = new Mock<IRequestSource>(MockBehavior.Strict);
        requestSourceMock
            .Setup(s => s.ReadAsync(It.IsAny<RequestQuery>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(requests ?? []));

        var tracker = new ReplayJobTracker(
            httpClientFactoryMock.Object,
            requestSourceMock.Object,
            options ?? new MemoaReplayApiOptions { TargetBaseUrl = "http://localhost" },
            NullLoggerFactory.Instance);

        return (tracker, handler);
    }

    private static async Task WaitForJobAsync(
        ReplayJobTracker tracker, Guid jobId, int timeoutMs = 5000)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (tracker.GetJob(jobId)?.Status is "Completed" or "Failed")
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"Job {jobId} did not complete within {timeoutMs}ms");
    }

    // ── StartJob ──────────────────────────────────────────────────────────────

    [Test]
    public async Task StartJob_ShouldReturnJobInfo_WithRunningStatus()
    {
        // Arrange
        var (tracker, _) = CreateTracker();

        // Act
        var info = tracker.StartJob(new ReplayRunRequest());

        // Assert
        info.Should().NotBeNull();
        info.JobId.Should().NotBeEmpty();
        info.Status.Should().Be("Running");
        info.StartedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        await WaitForJobAsync(tracker, info.JobId);
    }

    [Test]
    public async Task StartJob_ShouldAssignUniqueJobIds()
    {
        // Arrange
        var (tracker, _) = CreateTracker();

        // Act
        var job1 = tracker.StartJob(new ReplayRunRequest());
        var job2 = tracker.StartJob(new ReplayRunRequest());

        // Assert
        job1.JobId.Should().NotBe(job2.JobId);

        await WaitForJobAsync(tracker, job1.JobId);
        await WaitForJobAsync(tracker, job2.JobId);
    }

    // ── GetJob ────────────────────────────────────────────────────────────────

    [Test]
    public void GetJob_ShouldReturnNull_WhenJobNotFound()
    {
        // Arrange
        var (tracker, _) = CreateTracker();

        // Act
        var result = tracker.GetJob(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetJob_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var (tracker, _) = CreateTracker();
        var job = tracker.StartJob(new ReplayRunRequest());

        // Act — poll until job is known
        var result = tracker.GetJob(job.JobId);

        // Assert
        result.Should().NotBeNull();
        result!.JobId.Should().Be(job.JobId);

        await WaitForJobAsync(tracker, job.JobId);
    }

    // ── Job completion ────────────────────────────────────────────────────────

    [Test]
    public async Task ExecuteJob_ShouldCompleteJob_WithCorrectCounts()
    {
        // Arrange
        var requests = new[]
        {
            CreateRecordedRequest("GET", "/api/one"),
            CreateRecordedRequest("POST", "/api/two"),
            CreateRecordedRequest("PUT", "/api/three")
        };

        var (tracker, handler) = CreateTracker(requests: requests);

        // Act
        var job = tracker.StartJob(new ReplayRunRequest());
        await WaitForJobAsync(tracker, job.JobId);
        var result = tracker.GetJob(job.JobId);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.CompletedAtUtc.Should().NotBeNull();
        result.Result.Should().NotBeNull();
        result.Result!.Total.Should().Be(3);
        result.Result.Succeeded.Should().Be(3);
        result.Result.Failed.Should().Be(0);
        handler.CapturedRequests.Should().HaveCount(3);
    }

    [Test]
    public async Task ExecuteJob_ShouldSetDryRun_WhenRequestedInRunRequest()
    {
        // Arrange
        var (tracker, handler) = CreateTracker(
            requests: [CreateRecordedRequest()]);

        // Act
        var job = tracker.StartJob(new ReplayRunRequest { DryRun = true });
        await WaitForJobAsync(tracker, job.JobId);

        // Assert — dry run sends nothing
        handler.CapturedRequests.Should().BeEmpty();
        tracker.GetJob(job.JobId)!.Status.Should().Be("Completed");
    }

    [Test]
    public async Task ExecuteJob_ShouldUseTargetBaseUrlFromRequest_WhenProvided()
    {
        // Arrange — override target from request; handler is already at localhost
        var (tracker, handler) = CreateTracker(
            requests: [CreateRecordedRequest()]);

        // Act
        var job = tracker.StartJob(new ReplayRunRequest { TargetBaseUrl = "http://localhost" });
        await WaitForJobAsync(tracker, job.JobId);

        // Assert
        handler.CapturedRequests.Should().HaveCount(1);
    }

    // ── Authentication ────────────────────────────────────────────────────────

    [Test]
    public async Task ExecuteJob_ShouldApplyBearerToken_WhenProvidedInRunRequest()
    {
        // Arrange
        var (tracker, handler) = CreateTracker(requests: [CreateRecordedRequest()]);

        // Act
        var job = tracker.StartJob(new ReplayRunRequest { AuthBearerToken = "req-token" });
        await WaitForJobAsync(tracker, job.JobId);

        // Assert
        handler.CapturedRequests.Should().HaveCount(1);
        handler.CapturedRequests[0].Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequests[0].Headers.Authorization!.Parameter.Should().Be("req-token");
    }

    [Test]
    public async Task ExecuteJob_ShouldFallbackToServerAuth_WhenNoTokenInRequest()
    {
        // Arrange
        var options = new MemoaReplayApiOptions
        {
            TargetBaseUrl = "http://localhost",
            TargetAuthentication = new ReplayAuthentication { BearerToken = "server-token" }
        };
        var (tracker, handler) = CreateTracker(options: options, requests: [CreateRecordedRequest()]);

        // Act
        var job = tracker.StartJob(new ReplayRunRequest()); // no AuthBearerToken
        await WaitForJobAsync(tracker, job.JobId);

        // Assert
        handler.CapturedRequests[0].Headers.Authorization!.Parameter.Should().Be("server-token");
    }

    [Test]
    public async Task ExecuteJob_ShouldPreferRequestToken_OverServerDefault()
    {
        // Arrange
        var options = new MemoaReplayApiOptions
        {
            TargetBaseUrl = "http://localhost",
            TargetAuthentication = new ReplayAuthentication { BearerToken = "server-token" }
        };
        var (tracker, handler) = CreateTracker(options: options, requests: [CreateRecordedRequest()]);

        // Act
        var job = tracker.StartJob(new ReplayRunRequest { AuthBearerToken = "override-token" });
        await WaitForJobAsync(tracker, job.JobId);

        // Assert
        handler.CapturedRequests[0].Headers.Authorization!.Parameter.Should().Be("override-token");
    }
}
