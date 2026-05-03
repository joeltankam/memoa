using System.Text.Json;
using FluentAssertions;
using Memoa.Sinks.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace Memoa.Sinks.Redis.Tests;

[TestFixture(TestOf = typeof(RedisSink))]
internal class RedisSinkTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RecordedRequest CreateRequest(
        string method = "GET",
        string path = "/api/test",
        DateTimeOffset? capturedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        CapturedAtUtc = capturedAt ?? DateTimeOffset.UtcNow,
        Method = method,
        Scheme = "https",
        Host = "localhost",
        Path = path,
        Protocol = "HTTP/1.1"
    };

    private static StreamEntry ToStreamEntry(RecordedRequest request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new StreamEntry(
            "1-0",
            new NameValueEntry[]
            {
                new("id", request.Id.ToString()),
                new("timestamp", request.CapturedAtUtc.ToUnixTimeMilliseconds().ToString()),
                new("method", request.Method),
                new("path", request.Path),
                new("data", json)
            });
    }

    private static (RedisSink Sink, Mock<IDatabase> DbMock, Mock<IConnectionMultiplexer> RedisMock)
        CreateSink(RedisSinkOptions? options = null)
    {
        var dbMock = new Mock<IDatabase>(MockBehavior.Strict);
        var redisMock = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        var opts = options ?? new RedisSinkOptions { StreamKey = "memoa:requests" };

        redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(dbMock.Object);

        var sink = new RedisSink(redisMock.Object, opts, NullLogger<RedisSink>.Instance);
        return (sink, dbMock, redisMock);
    }

    // ── WriteAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task WriteAsync_ShouldAddEntriesToStream()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, dbMock, _) = CreateSink(new RedisSinkOptions
        {
            StreamKey = "memoa:requests",
            MaxLength = null
        });

        dbMock
            .Setup(d => d.StreamAddAsync(
                It.Is<RedisKey>(k => k == "memoa:requests"),
                It.Is<NameValueEntry[]>(e => e.Any(v => v.Name == "data")),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .Verifiable();

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert
        dbMock.VerifyAll();
    }

    [Test]
    public async Task WriteAsync_ShouldUseMaxLength_WhenConfigured()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, dbMock, _) = CreateSink(new RedisSinkOptions
        {
            StreamKey = "memoa:requests",
            MaxLength = 500
        });

        dbMock
            .Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(),
                It.Is<int?>(m => m == 500),
                It.Is<bool>(u => u == true),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .Verifiable();

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert
        dbMock.VerifyAll();
    }

    [Test]
    public async Task WriteAsync_ShouldApplyKeyPrefix_WhenConfigured()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, dbMock, _) = CreateSink(new RedisSinkOptions
        {
            StreamKey = "requests",
            KeyPrefix = "myapp",
            MaxLength = null
        });

        dbMock
            .Setup(d => d.StreamAddAsync(
                It.Is<RedisKey>(k => k == "myapp:requests"),
                It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .Verifiable();

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert
        dbMock.VerifyAll();
    }

    // ── ReadAsync ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ReadAsync_ShouldReturnEmpty_WhenStreamRangeReturnsNull()
    {
        // Arrange
        var (sink, dbMock, _) = CreateSink();

        dbMock
            .Setup(d => d.StreamRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((StreamEntry[]?)null!);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public async Task ReadAsync_ShouldReturnDeserializedRequests()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, dbMock, _) = CreateSink();

        dbMock
            .Setup(d => d.StreamRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([ToStreamEntry(request)]);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(request.Id);
        results[0].Method.Should().Be(request.Method);
    }

    [Test]
    public async Task ReadAsync_ShouldFilterByMethod()
    {
        // Arrange
        var getReq = CreateRequest(method: "GET");
        var postReq = CreateRequest(method: "POST");
        var (sink, dbMock, _) = CreateSink();

        dbMock
            .Setup(d => d.StreamRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([ToStreamEntry(getReq), ToStreamEntry(postReq)]);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery { Methods = ["POST"] }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle(r => r.Method == "POST");
    }

    [Test]
    public async Task ReadAsync_ShouldUseFromTime_AsStreamStartId()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedStartId = $"{from.ToUnixTimeMilliseconds()}-0";
        var (sink, dbMock, _) = CreateSink();

        dbMock
            .Setup(d => d.StreamRangeAsync(
                It.IsAny<RedisKey>(),
                It.Is<RedisValue?>(v => v.HasValue && v.Value == expectedStartId),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([])
            .Verifiable();

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery { From = from }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        dbMock.VerifyAll();
    }

    [Test]
    public async Task ReadAsync_ShouldUseToTime_AsStreamEndId()
    {
        // Arrange
        var to = new DateTimeOffset(2026, 5, 3, 12, 0, 0, TimeSpan.Zero);
        var expectedEndId = $"{to.ToUnixTimeMilliseconds()}-0";
        var (sink, dbMock, _) = CreateSink();

        dbMock
            .Setup(d => d.StreamRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue?>(),
                It.Is<RedisValue?>(v => v.HasValue && v.Value == expectedEndId),
                It.IsAny<int?>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([])
            .Verifiable();

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery { To = to }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        dbMock.VerifyAll();
    }

    [Test]
    public async Task ReadAsync_ShouldSkipEntriesWithMissingDataField()
    {
        // Arrange
        var (sink, dbMock, _) = CreateSink();
        var badEntry = new StreamEntry("2-0", [new NameValueEntry("other", "value")]);

        dbMock
            .Setup(d => d.StreamRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([badEntry]);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().BeEmpty();
    }

    [Test]
    public async Task ReadAsync_ShouldSkipEntriesWithInvalidJson()
    {
        // Arrange
        var valid = CreateRequest();
        var (sink, dbMock, _) = CreateSink();

        var badEntry = new StreamEntry("1-0", [new NameValueEntry("data", "not-json")]);
        var goodEntry = ToStreamEntry(valid);

        dbMock
            .Setup(d => d.StreamRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<Order>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([badEntry, goodEntry]);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert — invalid entry skipped, valid one returned
        results.Should().ContainSingle(r => r.Id == valid.Id);
    }
}
