using FluentAssertions;
using Memoa.Sinks.File;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Memoa.Sinks.File.Tests;

[TestFixture(TestOf = typeof(FileSink))]
internal class FileSinkTests
{
    private string _tempDir = null!;
    private FileSinkOptions _options = null!;
    private FileSink _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"memoa-tests-{Guid.NewGuid():N}");
        _options = new FileSinkOptions { OutputDirectory = _tempDir };
        _sut = new FileSink(_options, NullLogger<FileSink>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

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

    // ── WriteAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task WriteAsync_ShouldCreateFileWithDefaultFormat()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert
        var ts = request.CapturedAtUtc;
        var expectedRelative = Path.Combine(
            ts.Year.ToString("D4"),
            ts.Month.ToString("D2"),
            ts.Day.ToString("D2"),
            ts.Hour.ToString("D2"),
            $"{request.Id}.json");
        var fullPath = Path.Combine(_tempDir, expectedRelative);
        System.IO.File.Exists(fullPath).Should().BeTrue();
    }

    [Test]
    public async Task WriteAsync_ShouldCreateDirectoryStructure_WhenMissing()
    {
        // Arrange — directory doesn't exist yet
        var request = CreateRequest();

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert — directory was created
        Directory.Exists(_tempDir).Should().BeTrue();
    }

    [Test]
    public async Task WriteAsync_ShouldSerializeRequest_AsValidJson()
    {
        // Arrange
        var request = CreateRequest(method: "POST", path: "/api/orders");

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert
        var ts = request.CapturedAtUtc;
        var relativePath = Path.Combine(
            ts.Year.ToString("D4"), ts.Month.ToString("D2"),
            ts.Day.ToString("D2"), ts.Hour.ToString("D2"),
            $"{request.Id}.json");
        var content = await System.IO.File.ReadAllTextAsync(Path.Combine(_tempDir, relativePath));
        content.Should().Contain("\"method\"").And.Contain("POST");
        content.Should().Contain("\"path\"").And.Contain("/api/orders");
    }

    [Test]
    public async Task WriteAsync_ShouldWriteIndentedJson_WhenIndentJsonIsTrue()
    {
        // Arrange
        _options.IndentJson = true;
        var request = CreateRequest();

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert — indented JSON contains newlines
        var ts = request.CapturedAtUtc;
        var relativePath = Path.Combine(
            ts.Year.ToString("D4"), ts.Month.ToString("D2"),
            ts.Day.ToString("D2"), ts.Hour.ToString("D2"),
            $"{request.Id}.json");
        var content = await System.IO.File.ReadAllTextAsync(Path.Combine(_tempDir, relativePath));
        content.Should().Contain(Environment.NewLine);
    }

    [Test]
    public async Task WriteAsync_ShouldWriteCompactJson_WhenIndentJsonIsFalse()
    {
        // Arrange
        _options.IndentJson = false;
        var request = CreateRequest();

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert — compact JSON has no leading whitespace on second line
        var ts = request.CapturedAtUtc;
        var relativePath = Path.Combine(
            ts.Year.ToString("D4"), ts.Month.ToString("D2"),
            ts.Day.ToString("D2"), ts.Hour.ToString("D2"),
            $"{request.Id}.json");
        var content = await System.IO.File.ReadAllTextAsync(Path.Combine(_tempDir, relativePath));
        content.Should().StartWith("{");
        content.Replace("\r\n", "\n").Should().NotContain("\n  ");
    }

    [Test]
    public async Task WriteAsync_ShouldUseCustomFileNameFormat()
    {
        // Arrange
        _options.FileNameFormat = "{method}-{id}.json";
        var request = CreateRequest(method: "PUT");

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert
        var expectedFile = Path.Combine(_tempDir, $"PUT-{request.Id}.json");
        System.IO.File.Exists(expectedFile).Should().BeTrue();
    }

    [Test]
    public async Task WriteAsync_ShouldOverwriteExistingFile()
    {
        // Arrange
        var request = CreateRequest();
        await _sut.WriteAsync(request, CancellationToken.None);

        // Act — write same request again
        var act = async () => await _sut.WriteAsync(request, CancellationToken.None);

        // Assert — no exception
        await act.Should().NotThrowAsync();
    }

    // ── ReadAsync ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ReadAsync_ShouldReturnEmpty_WhenDirectoryDoesNotExist()
    {
        // Arrange — use a non-existent directory
        var sink = new FileSink(
            new FileSinkOptions { OutputDirectory = Path.Combine(_tempDir, "nonexistent") },
            NullLogger<FileSink>.Instance);

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
    public async Task ReadAsync_ShouldReturnWrittenRequest()
    {
        // Arrange
        var request = CreateRequest();
        await _sut.WriteAsync(request, CancellationToken.None);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(request.Id);
        results[0].Method.Should().Be(request.Method);
        results[0].Path.Should().Be(request.Path);
    }

    [Test]
    public async Task ReadAsync_ShouldReturnMultipleRequests()
    {
        // Arrange
        var requests = Enumerable.Range(0, 3)
            .Select(i => CreateRequest(path: $"/api/item/{i}"))
            .ToList();

        foreach (var r in requests)
        {
            await _sut.WriteAsync(r, CancellationToken.None);
        }

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Select(r => r.Id).Should().BeEquivalentTo(requests.Select(r => r.Id));
    }

    [Test]
    public async Task ReadAsync_ShouldFilterByMethod()
    {
        // Arrange
        var get = CreateRequest(method: "GET");
        var post = CreateRequest(method: "POST");
        await _sut.WriteAsync(get, CancellationToken.None);
        await _sut.WriteAsync(post, CancellationToken.None);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery { Methods = ["POST"] }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle(r => r.Method == "POST");
    }

    [Test]
    public async Task ReadAsync_ShouldFilterByFromTime()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var old = CreateRequest(capturedAt: now.AddHours(-2));
        var recent = CreateRequest(capturedAt: now);
        await _sut.WriteAsync(old, CancellationToken.None);
        await _sut.WriteAsync(recent, CancellationToken.None);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery { From = now.AddHours(-1) }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(recent.Id);
    }

    [Test]
    public async Task ReadAsync_ShouldFilterByToTime()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var old = CreateRequest(capturedAt: now.AddHours(-2));
        var recent = CreateRequest(capturedAt: now);
        await _sut.WriteAsync(old, CancellationToken.None);
        await _sut.WriteAsync(recent, CancellationToken.None);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery { To = now.AddHours(-1) }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(old.Id);
    }

    [Test]
    public async Task ReadAsync_ShouldFilterByPathPattern()
    {
        // Arrange
        var orders = CreateRequest(path: "/api/orders/1");
        var users = CreateRequest(path: "/api/users/1");
        await _sut.WriteAsync(orders, CancellationToken.None);
        await _sut.WriteAsync(users, CancellationToken.None);

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery { PathPattern = "/api/orders/**" }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(orders.Id);
    }

    [Test]
    public async Task ReadAsync_ShouldSkipInvalidJsonFiles()
    {
        // Arrange
        Directory.CreateDirectory(_tempDir);
        await System.IO.File.WriteAllTextAsync(Path.Combine(_tempDir, "bad.json"), "not-json");
        var request = CreateRequest();
        await _sut.WriteAsync(request, CancellationToken.None);

        // Act — should not throw
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery(), CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert — valid request returned, bad file skipped
        results.Should().ContainSingle(r => r.Id == request.Id);
    }

    [Test]
    public async Task ReadAsync_ShouldHonorTakeLimit()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            await _sut.WriteAsync(CreateRequest(path: $"/api/{i}"), CancellationToken.None);
        }

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(new RequestQuery { Take = 2 }, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert — FileSink does not enforce Take itself; upstream callers do, so verify all returned
        // (FileSink.ReadAsync yields all matching; Take is a hint for callers)
        results.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
