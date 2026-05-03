using Azure.Storage.Blobs;
using FluentAssertions;
using Memoa.Sinks.AzureBlobStorage;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Memoa.Sinks.AzureBlobStorage.Tests;

/// <summary>
/// Integration tests for <see cref="AzureBlobStorageSink"/> using Azurite.
/// Requires Azurite running on default ports (10000 for blob).
/// </summary>
[TestFixture(TestOf = typeof(AzureBlobStorageSink))]
[Category("Azurite")]
internal class AzureBlobStorageSinkTests
{
    private const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    private BlobContainerClient _containerClient = null!;
    private AzureBlobStorageSinkOptions _options = null!;
    private AzureBlobStorageSink _sut = null!;

    [SetUp]
    public async Task SetUp()
    {
        _options = new AzureBlobStorageSinkOptions
        {
            ContainerName = $"test-{Guid.NewGuid():N}",
            CreateContainerIfNotExists = true
        };

        _containerClient = new BlobContainerClient(AzuriteConnectionString, _options.ContainerName);
        _sut = new AzureBlobStorageSink(_containerClient, _options, NullLogger<AzureBlobStorageSink>.Instance);

        await _containerClient.CreateIfNotExistsAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _containerClient.DeleteIfExistsAsync();
    }

    [Test]
    public async Task WriteAsync_ShouldCreateBlobWithExpectedName()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert
        var ts = request.CapturedAtUtc;
        var expectedName = $"{ts.Year:D4}/{ts.Month:D2}/{ts.Day:D2}/{ts.Hour:D2}/{request.Id}.json";
        var blobClient = _containerClient.GetBlobClient(expectedName);
        var exists = await blobClient.ExistsAsync();
        exists.Value.Should().BeTrue();
    }

    [Test]
    public async Task WriteAsync_ShouldApplyBlobPrefix()
    {
        // Arrange
        _options.BlobPrefix = "my-prefix";
        var request = CreateRequest();

        // Act
        await _sut.WriteAsync(request, CancellationToken.None);

        // Assert
        var ts = request.CapturedAtUtc;
        var expectedName = $"my-prefix/{ts.Year:D4}/{ts.Month:D2}/{ts.Day:D2}/{ts.Hour:D2}/{request.Id}.json";
        var blobClient = _containerClient.GetBlobClient(expectedName);
        var exists = await blobClient.ExistsAsync();
        exists.Value.Should().BeTrue();
    }

    [Test]
    public async Task ReadAsync_ShouldReturnWrittenRequests()
    {
        // Arrange
        var request = CreateRequest();
        await _sut.WriteAsync(request, CancellationToken.None);

        var query = new RequestQuery();

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(query, CancellationToken.None))
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
    public async Task ReadAsync_ShouldFilterByMethod()
    {
        // Arrange
        var getRequest = CreateRequest(method: "GET");
        var postRequest = CreateRequest(method: "POST");
        await _sut.WriteAsync(getRequest, CancellationToken.None);
        await _sut.WriteAsync(postRequest, CancellationToken.None);

        var query = new RequestQuery { Methods = ["POST"] };

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(query, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Method.Should().Be("POST");
    }

    [Test]
    public async Task ReadAsync_ShouldFilterByTimeRange()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var oldRequest = CreateRequest(capturedAt: now.AddHours(-2));
        var newRequest = CreateRequest(capturedAt: now);
        await _sut.WriteAsync(oldRequest, CancellationToken.None);
        await _sut.WriteAsync(newRequest, CancellationToken.None);

        var query = new RequestQuery { From = now.AddHours(-1) };

        // Act
        var results = new List<RecordedRequest>();
        await foreach (var r in _sut.ReadAsync(query, CancellationToken.None))
        {
            results.Add(r);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(newRequest.Id);
    }

    [Test]
    public async Task WriteAsync_ShouldOverwriteExistingBlob()
    {
        // Arrange
        var request = CreateRequest();
        await _sut.WriteAsync(request, CancellationToken.None);

        // Act — write again (same ID)
        var act = async () => await _sut.WriteAsync(request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    private static RecordedRequest CreateRequest(
        string method = "GET",
        string path = "/api/test",
        DateTimeOffset? capturedAt = null)
    {
        return new RecordedRequest
        {
            Id = Guid.NewGuid(),
            CapturedAtUtc = capturedAt ?? DateTimeOffset.UtcNow,
            Method = method,
            Scheme = "https",
            Host = "localhost",
            Path = path,
            Protocol = "HTTP/1.1"
        };
    }
}
