using FluentAssertions;
using Memoa;
using NUnit.Framework;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Memoa;

namespace Serilog.Sinks.Memoa.Tests;

[TestFixture]
public sealed class MemoaSerilogSinkTests
{
    private List<LogEvent> _logEvents = null!;
    private ILogger _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logEvents = [];
        _logger = new LoggerConfiguration()
            .WriteTo.Sink(new DelegatingSink(e => _logEvents.Add(e)))
            .CreateLogger();
    }

    [TearDown]
    public void TearDown()
    {
        (_logger as IDisposable)?.Dispose();
    }

    [Test]
    public async Task WriteAsync_EmitsLogEvent_WithRequestProperties()
    {
        var sink = new MemoaSerilogSink(_logger);
        var request = CreateRequest();

        await sink.WriteAsync(request, CancellationToken.None);

        _logEvents.Should().HaveCount(1);
        var logEvent = _logEvents[0];
        logEvent.Level.Should().Be(LogEventLevel.Information);
        logEvent.Properties["Method"].ToString().Should().Contain("POST");
        logEvent.Properties["Path"].ToString().Should().Contain("/api/test");
    }

    [Test]
    public async Task WriteAsync_IncludesRequestBody_WhenEnabled()
    {
        var sink = new MemoaSerilogSink(_logger, new MemoaSinkOptions { IncludeRequestBody = true });
        var request = CreateRequest(body: "hello world");

        await sink.WriteAsync(request, CancellationToken.None);

        _logEvents[0].Properties.Should().ContainKey("RequestBody");
        _logEvents[0].Properties["RequestBody"].ToString().Should().Contain("hello world");
    }

    [Test]
    public async Task WriteAsync_ExcludesRequestBody_WhenDisabled()
    {
        var sink = new MemoaSerilogSink(_logger, new MemoaSinkOptions { IncludeRequestBody = false });
        var request = CreateRequest(body: "hello world");

        await sink.WriteAsync(request, CancellationToken.None);

        _logEvents[0].Properties.Should().NotContainKey("RequestBody");
    }

    [Test]
    public async Task WriteAsync_TruncatesBody_WhenExceedingMaxLength()
    {
        var sink = new MemoaSerilogSink(_logger, new MemoaSinkOptions { MaxBodyLength = 10 });
        var request = CreateRequest(body: "this is a very long body");

        await sink.WriteAsync(request, CancellationToken.None);

        var bodyValue = _logEvents[0].Properties["RequestBody"].ToString();
        bodyValue.Should().Contain("[truncated]");
    }

    [Test]
    public async Task WriteAsync_IncludesHeaders_WhenEnabled()
    {
        var sink = new MemoaSerilogSink(_logger, new MemoaSinkOptions { IncludeHeaders = true });
        var request = CreateRequest();

        await sink.WriteAsync(request, CancellationToken.None);

        _logEvents[0].Properties.Should().ContainKey("Headers");
    }

    [Test]
    public async Task WriteAsync_ExcludesHeaders_WhenDisabled()
    {
        var sink = new MemoaSerilogSink(_logger, new MemoaSinkOptions { IncludeHeaders = false });
        var request = CreateRequest();

        await sink.WriteAsync(request, CancellationToken.None);

        _logEvents[0].Properties.Should().NotContainKey("Headers");
    }

    [Test]
    public async Task WriteAsync_IncludesResponse_WhenPresent()
    {
        var sink = new MemoaSerilogSink(_logger);
        var request = CreateRequest(includeResponse: true);

        await sink.WriteAsync(request, CancellationToken.None);

        _logEvents[0].Properties.Should().ContainKey("StatusCode");
        _logEvents[0].Properties["StatusCode"].ToString().Should().Be("200");
    }

    [Test]
    public async Task WriteAsync_DoesNotEmit_WhenLevelDisabled()
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Fatal()
            .WriteTo.Sink(new DelegatingSink(e => _logEvents.Add(e)))
            .CreateLogger();

        var sink = new MemoaSerilogSink(logger, new MemoaSinkOptions { Level = LogEventLevel.Information });
        var request = CreateRequest();

        await sink.WriteAsync(request, CancellationToken.None);

        _logEvents.Should().BeEmpty();
    }

    private static RecordedRequest CreateRequest(string? body = null, bool includeResponse = false)
    {
        return new RecordedRequest
        {
            Id = Guid.NewGuid(),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Method = "POST",
            Scheme = "https",
            Host = "localhost",
            Path = "/api/test",
            QueryString = "?q=1",
            Protocol = "HTTP/2",
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = ["application/json"],
                ["X-Request-Id"] = ["abc-123"]
            },
            ClientIp = "127.0.0.1",
            Body = body is not null
                ? new RecordedBody { ContentType = "application/json", Length = body.Length, Text = body }
                : null,
            Response = includeResponse
                ? new RecordedResponse { StatusCode = 200, ElapsedMs = 42.5 }
                : null
        };
    }

    /// <summary>
    /// A simple Serilog sink that delegates to an action for test assertions.
    /// </summary>
    private sealed class DelegatingSink : Serilog.Core.ILogEventSink
    {
        private readonly Action<LogEvent> _write;

        public DelegatingSink(Action<LogEvent> write) => _write = write;

        public void Emit(LogEvent logEvent) => _write(logEvent);
    }
}
