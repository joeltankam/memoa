using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Memoa.Sinks.AmazonS3;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Memoa.Sinks.AmazonS3.Tests;

[TestFixture(TestOf = typeof(AmazonS3Sink))]
internal class AmazonS3SinkTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RecordedRequest CreateRequest(
        string method = "GET",
        string path = "/api/test",
        DateTimeOffset? capturedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        CapturedAtUtc = capturedAt ?? new DateTimeOffset(2026, 5, 3, 14, 0, 0, TimeSpan.Zero),
        Method = method,
        Scheme = "https",
        Host = "localhost",
        Path = path,
        Protocol = "HTTP/1.1"
    };

    private static Stream JsonStream(RecordedRequest request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    private static (AmazonS3Sink Sink, Mock<IAmazonS3> S3Mock) CreateSink(
        AmazonS3SinkOptions? options = null)
    {
        var s3Mock = new Mock<IAmazonS3>(MockBehavior.Strict);
        var opts = options ?? new AmazonS3SinkOptions
        {
            BucketName = "test-bucket",
            CreateBucketIfNotExists = false
        };

        var sink = new AmazonS3Sink(s3Mock.Object, opts, NullLogger<AmazonS3Sink>.Instance);
        return (sink, s3Mock);
    }

    // ── WriteAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task WriteAsync_ShouldPutObjectWithExpectedKey()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, s3Mock) = CreateSink();
        var ts = request.CapturedAtUtc;
        var expectedKey = $"{ts.Year:D4}/{ts.Month:D2}/{ts.Day:D2}/{ts.Hour:D2}/{request.Id}.json";

        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.Is<PutObjectRequest>(r => r.Key == expectedKey && r.BucketName == "test-bucket"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse())
            .Verifiable();

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert
        s3Mock.VerifyAll();
    }

    [Test]
    public async Task WriteAsync_ShouldApplyKeyPrefix_WhenConfigured()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, s3Mock) = CreateSink(new AmazonS3SinkOptions
        {
            BucketName = "test-bucket",
            KeyPrefix = "my-prefix",
            CreateBucketIfNotExists = false
        });
        var ts = request.CapturedAtUtc;
        var expectedKey = $"my-prefix/{ts.Year:D4}/{ts.Month:D2}/{ts.Day:D2}/{ts.Hour:D2}/{request.Id}.json";

        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.Is<PutObjectRequest>(r => r.Key == expectedKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse())
            .Verifiable();

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert
        s3Mock.VerifyAll();
    }

    [Test]
    public async Task WriteAsync_ShouldUseCustomKeyFormat()
    {
        // Arrange
        var request = CreateRequest(method: "POST");
        var (sink, s3Mock) = CreateSink(new AmazonS3SinkOptions
        {
            BucketName = "test-bucket",
            KeyFormat = "{method}/{id}.json",
            CreateBucketIfNotExists = false
        });
        var expectedKey = $"POST/{request.Id}.json";

        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.Is<PutObjectRequest>(r => r.Key == expectedKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse())
            .Verifiable();

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert
        s3Mock.VerifyAll();
    }

    [Test]
    public async Task WriteAsync_ShouldSetContentTypeToJson()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, s3Mock) = CreateSink();

        s3Mock
            .Setup(s => s.PutObjectAsync(
                It.Is<PutObjectRequest>(r => r.ContentType == "application/json"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse())
            .Verifiable();

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert
        s3Mock.VerifyAll();
    }

    [Test]
    public async Task WriteAsync_ShouldCreateBucket_WhenCreateBucketIfNotExistsIsTrue()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, s3Mock) = CreateSink(new AmazonS3SinkOptions
        {
            BucketName = "test-bucket",
            CreateBucketIfNotExists = true
        });

        // Sink calls PutBucketAsync directly and ignores "already exists" errors
        s3Mock
            .Setup(s => s.PutBucketAsync(
                It.Is<PutBucketRequest>(r => r.BucketName == "test-bucket"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutBucketResponse())
            .Verifiable();

        s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert — bucket creation was attempted
        s3Mock.Verify(s => s.PutBucketAsync(
            It.Is<PutBucketRequest>(r => r.BucketName == "test-bucket"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task WriteAsync_ShouldSkipBucketCreation_WhenCreateBucketIfNotExistsIsFalse()
    {
        // Arrange
        var request = CreateRequest();
        var (sink, s3Mock) = CreateSink(new AmazonS3SinkOptions
        {
            BucketName = "test-bucket",
            CreateBucketIfNotExists = false
        });

        s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        await sink.WriteAsync(request, CancellationToken.None);

        // Assert — PutBucket never called when opt-out
        s3Mock.Verify(
            s => s.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── ReadAsync ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ReadAsync_ShouldReturnEmpty_WhenNoObjectsInBucket()
    {
        // Arrange
        var (sink, s3Mock) = CreateSink();

        s3Mock
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = [], IsTruncated = false });

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
        var (sink, s3Mock) = CreateSink();

        s3Mock
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = [new S3Object { Key = "key.json" }],
                IsTruncated = false
            });

        var getResponse = new GetObjectResponse { ResponseStream = JsonStream(request) };
        s3Mock
            .Setup(s => s.GetObjectAsync(
                It.Is<GetObjectRequest>(r => r.Key == "key.json"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getResponse);

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
        var (sink, s3Mock) = CreateSink();

        s3Mock
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = [new S3Object { Key = "get.json" }, new S3Object { Key = "post.json" }],
                IsTruncated = false
            });

        s3Mock
            .Setup(s => s.GetObjectAsync(
                It.Is<GetObjectRequest>(r => r.Key == "get.json"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = JsonStream(getReq) });

        s3Mock
            .Setup(s => s.GetObjectAsync(
                It.Is<GetObjectRequest>(r => r.Key == "post.json"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = JsonStream(postReq) });

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
    public async Task ReadAsync_ShouldFilterByTimeRange()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 3, 14, 0, 0, TimeSpan.Zero);
        var oldReq = CreateRequest(capturedAt: now.AddHours(-2));
        var recentReq = CreateRequest(capturedAt: now);
        var (sink, s3Mock) = CreateSink();

        s3Mock
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = [new S3Object { Key = "old.json" }, new S3Object { Key = "new.json" }],
                IsTruncated = false
            });

        s3Mock
            .Setup(s => s.GetObjectAsync(It.Is<GetObjectRequest>(r => r.Key == "old.json"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = JsonStream(oldReq) });

        s3Mock
            .Setup(s => s.GetObjectAsync(It.Is<GetObjectRequest>(r => r.Key == "new.json"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = JsonStream(recentReq) });

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery { From = now.AddHours(-1) }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(recentReq.Id);
    }

    [Test]
    public async Task ReadAsync_ShouldSkipUndeserializableObjects()
    {
        // Arrange
        var validReq = CreateRequest();
        var (sink, s3Mock) = CreateSink();

        s3Mock
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = [new S3Object { Key = "bad.json" }, new S3Object { Key = "good.json" }],
                IsTruncated = false
            });

        // bad.json throws
        s3Mock
            .Setup(s => s.GetObjectAsync(It.Is<GetObjectRequest>(r => r.Key == "bad.json"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("read error"));

        s3Mock
            .Setup(s => s.GetObjectAsync(It.Is<GetObjectRequest>(r => r.Key == "good.json"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = JsonStream(validReq) });

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert — bad object skipped, valid one returned
        results.Should().ContainSingle(r => r.Id == validReq.Id);
    }

    [Test]
    public async Task ReadAsync_ShouldHandlePaginatedResults()
    {
        // Arrange
        var req1 = CreateRequest(path: "/api/page1");
        var req2 = CreateRequest(path: "/api/page2");
        var (sink, s3Mock) = CreateSink();
        var callCount = 0;

        s3Mock
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new ListObjectsV2Response
                    {
                        S3Objects = [new S3Object { Key = "page1.json" }],
                        IsTruncated = true,
                        NextContinuationToken = "token-2"
                    };
                }

                return new ListObjectsV2Response
                {
                    S3Objects = [new S3Object { Key = "page2.json" }],
                    IsTruncated = false
                };
            });

        s3Mock
            .Setup(s => s.GetObjectAsync(It.Is<GetObjectRequest>(r => r.Key == "page1.json"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = JsonStream(req1) });

        s3Mock
            .Setup(s => s.GetObjectAsync(It.Is<GetObjectRequest>(r => r.Key == "page2.json"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = JsonStream(req2) });

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in sink.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert — both pages returned, ListObjectsV2 called twice
        results.Should().HaveCount(2);
        callCount.Should().Be(2);
    }
}
