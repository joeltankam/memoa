using FluentAssertions;
using Memoa.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(BackgroundRequestPipeline))]
internal class BackgroundRequestPipelineTests
{
    [Test]
    public async Task SubmitAsync_ShouldDeliverRequestToSink()
    {
        // Arrange
        var delivered = new TaskCompletionSource<RecordedRequest>();
        var sinkMock = new Mock<IRequestSink>(MockBehavior.Strict);
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((r, _) => delivered.SetResult(r))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var options = new MemoaOptions
        {
            Pipeline = new MemoaPipelineOptions
            {
                Mode = PipelineMode.Background,
                ChannelCapacity = 10,
                WorkerCount = 1
            }
        };

        using var sut = new BackgroundRequestPipeline(
            [sinkMock.Object],
            Options.Create(options),
            NullLogger<BackgroundRequestPipeline>.Instance);

        var request = CreateRecordedRequest();

        // Act
        await sut.StartAsync(CancellationToken.None);
        await sut.SubmitAsync(request, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await delivered.Task.WaitAsync(cts.Token);

        await sut.StopAsync(CancellationToken.None);

        // Assert
        result.Id.Should().Be(request.Id);
        sinkMock.VerifyAll();
    }

    [Test]
    public async Task StopAsync_ShouldDrainChannel()
    {
        // Arrange
        var count = 0;
        var sinkMock = new Mock<IRequestSink>(MockBehavior.Strict);
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((_, _) => Interlocked.Increment(ref count))
            .Returns(ValueTask.CompletedTask);

        var options = new MemoaOptions
        {
            Pipeline = new MemoaPipelineOptions
            {
                ChannelCapacity = 100,
                WorkerCount = 1,
                ShutdownTimeout = TimeSpan.FromSeconds(5)
            }
        };

        using var sut = new BackgroundRequestPipeline(
            [sinkMock.Object],
            Options.Create(options),
            NullLogger<BackgroundRequestPipeline>.Instance);

        // Act
        await sut.StartAsync(CancellationToken.None);
        for (var i = 0; i < 10; i++)
        {
            await sut.SubmitAsync(CreateRecordedRequest(), CancellationToken.None);
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        count.Should().Be(10);
    }

    [Test]
    public async Task SubmitAsync_ShouldNotThrow_WhenChannelIsFullAndModeIsDropWrite()
    {
        // Arrange — tiny channel, no consumers running
        var options = new MemoaOptions
        {
            Pipeline = new MemoaPipelineOptions
            {
                ChannelCapacity = 1,
                FullMode = ChannelFullMode.DropWrite,
                WorkerCount = 1
            }
        };

        var sinkMock = new Mock<IRequestSink>(MockBehavior.Strict);

        using var sut = new BackgroundRequestPipeline(
            [sinkMock.Object],
            Options.Create(options),
            NullLogger<BackgroundRequestPipeline>.Instance);

        // Do NOT start the pipeline — channel will fill up

        // Act — fill the channel, then try one more
        await sut.SubmitAsync(CreateRecordedRequest(), CancellationToken.None);

        var act = async () => await sut.SubmitAsync(CreateRecordedRequest(), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Pipeline_ShouldContinueProcessing_WhenSinkThrows()
    {
        // Arrange
        var callCount = 0;
        var sinkMock = new Mock<IRequestSink>(MockBehavior.Strict);
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((_, _) =>
            {
                var current = Interlocked.Increment(ref callCount);
                if (current == 1)
                {
                    throw new InvalidOperationException("Transient failure");
                }
            })
            .Returns(ValueTask.CompletedTask);

        var secondDelivered = new TaskCompletionSource();
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecordedRequest, CancellationToken>((_, _) =>
            {
                var current = Interlocked.Increment(ref callCount);
                if (current == 1)
                {
                    throw new InvalidOperationException("Transient failure");
                }

                secondDelivered.TrySetResult();
            })
            .Returns(ValueTask.CompletedTask);

        var options = new MemoaOptions
        {
            Pipeline = new MemoaPipelineOptions
            {
                ChannelCapacity = 10,
                WorkerCount = 1
            }
        };

        using var sut = new BackgroundRequestPipeline(
            [sinkMock.Object],
            Options.Create(options),
            NullLogger<BackgroundRequestPipeline>.Instance);

        // Act
        await sut.StartAsync(CancellationToken.None);
        await sut.SubmitAsync(CreateRecordedRequest(), CancellationToken.None);
        await sut.SubmitAsync(CreateRecordedRequest(), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await secondDelivered.Task.WaitAsync(cts.Token);

        await sut.StopAsync(CancellationToken.None);

        // Assert — both requests were processed
        callCount.Should().BeGreaterThanOrEqualTo(2);
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
