using FluentAssertions;
using Memoa.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(InlineRequestPipeline))]
internal class InlineRequestPipelineTests
{
    [Test]
    public async Task SubmitAsync_ShouldWriteToAllSinks()
    {
        // Arrange
        var request = CreateRecordedRequest();
        var cancellationToken = new CancellationTokenSource().Token;

        var sink1Mock = new Mock<IRequestSink>(MockBehavior.Strict);
        sink1Mock
            .Setup(s => s.WriteAsync(It.Is<RecordedRequest>(r => r.Id == request.Id), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var sink2Mock = new Mock<IRequestSink>(MockBehavior.Strict);
        sink2Mock
            .Setup(s => s.WriteAsync(It.Is<RecordedRequest>(r => r.Id == request.Id), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var sut = new InlineRequestPipeline(
            [sink1Mock.Object, sink2Mock.Object],
            NullLogger<InlineRequestPipeline>.Instance);

        // Act
        await sut.SubmitAsync(request, cancellationToken);

        // Assert
        sink1Mock.VerifyAll();
        sink2Mock.VerifyAll();
    }

    [Test]
    public async Task SubmitAsync_ShouldContinueToNextSink_WhenOneFails()
    {
        // Arrange
        var request = CreateRecordedRequest();

        var failingSinkMock = new Mock<IRequestSink>(MockBehavior.Strict);
        failingSinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sink failure"))
            .Verifiable();

        var successSinkMock = new Mock<IRequestSink>(MockBehavior.Strict);
        successSinkMock
            .Setup(s => s.WriteAsync(It.Is<RecordedRequest>(r => r.Id == request.Id), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var sut = new InlineRequestPipeline(
            [failingSinkMock.Object, successSinkMock.Object],
            NullLogger<InlineRequestPipeline>.Instance);

        // Act
        await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        failingSinkMock.VerifyAll();
        successSinkMock.VerifyAll();
    }

    [Test]
    public async Task SubmitAsync_ShouldHandleNoSinks()
    {
        // Arrange
        var sut = new InlineRequestPipeline(
            [],
            NullLogger<InlineRequestPipeline>.Instance);

        // Act
        var act = async () => await sut.SubmitAsync(CreateRecordedRequest(), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    private static RecordedRequest CreateRecordedRequest()
    {
        return new RecordedRequest
        {
            Id = Guid.NewGuid(),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Method = "GET",
            Scheme = "https",
            Host = "localhost",
            Path = "/api/test",
            Protocol = "HTTP/1.1"
        };
    }
}
