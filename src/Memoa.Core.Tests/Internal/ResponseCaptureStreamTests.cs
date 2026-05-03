using FluentAssertions;
using Memoa.Internal;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(ResponseCaptureStream))]
internal class ResponseCaptureStreamTests
{
    [Test]
    public async Task WriteAsync_ShouldForwardToInnerStream()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var sut = new ResponseCaptureStream(inner, 1024);
        var data = "Hello, World!"u8.ToArray();

        // Act
        await sut.WriteAsync(data);
        await sut.FlushAsync();

        // Assert
        inner.ToArray().Should().Equal(data);
    }

    [Test]
    public async Task WriteAsync_ShouldCaptureWrittenBytes()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var sut = new ResponseCaptureStream(inner, 1024);
        var data = "Hello, World!"u8.ToArray();

        // Act
        await sut.WriteAsync(data);

        // Assert
        sut.GetCapturedBytes().Should().Equal(data);
        sut.Truncated.Should().BeFalse();
    }

    [Test]
    public async Task WriteAsync_ShouldTruncateCapture_WhenExceedingMaxSize()
    {
        // Arrange
        using var inner = new MemoryStream();
        var maxSize = 5;
        using var sut = new ResponseCaptureStream(inner, maxSize);
        var data = "Hello, World!"u8.ToArray();

        // Act
        await sut.WriteAsync(data);

        // Assert
        sut.GetCapturedBytes().Should().HaveCount(maxSize);
        sut.GetCapturedBytes().Should().Equal("Hello"u8.ToArray());
        sut.Truncated.Should().BeTrue();
    }

    [Test]
    public async Task WriteAsync_ShouldStillForwardAllDataToInner_WhenTruncated()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var sut = new ResponseCaptureStream(inner, 5);
        var data = "Hello, World!"u8.ToArray();

        // Act
        await sut.WriteAsync(data);
        await sut.FlushAsync();

        // Assert — inner stream gets ALL the data
        inner.ToArray().Should().Equal(data);
        // But capture is truncated
        sut.GetCapturedBytes().Should().HaveCount(5);
    }

    [Test]
    public async Task WriteAsync_ShouldAccumulateMultipleWrites()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var sut = new ResponseCaptureStream(inner, 1024);

        // Act
        await sut.WriteAsync("Hello"u8.ToArray());
        await sut.WriteAsync(", "u8.ToArray());
        await sut.WriteAsync("World!"u8.ToArray());

        // Assert
        sut.GetCapturedBytes().Should().Equal("Hello, World!"u8.ToArray());
    }

    [Test]
    public async Task WriteAsync_ShouldTruncateAcrossMultipleWrites()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var sut = new ResponseCaptureStream(inner, 8);

        // Act
        await sut.WriteAsync("Hello"u8.ToArray()); // 5 bytes
        await sut.WriteAsync(", World!"u8.ToArray()); // 8 bytes, only 3 fit

        // Assert
        sut.GetCapturedBytes().Should().Equal("Hello, W"u8.ToArray());
        sut.Truncated.Should().BeTrue();
    }

    [Test]
    public void Write_ShouldCaptureAndForward()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var sut = new ResponseCaptureStream(inner, 1024);
        var data = "Test"u8.ToArray();

        // Act
        sut.Write(data, 0, data.Length);

        // Assert
        inner.ToArray().Should().Equal(data);
        sut.GetCapturedBytes().Should().Equal(data);
    }

    [Test]
    public void Properties_ShouldDelegateToInnerStream()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var sut = new ResponseCaptureStream(inner, 1024);

        // Assert
        sut.CanRead.Should().Be(inner.CanRead);
        sut.CanSeek.Should().Be(inner.CanSeek);
        sut.CanWrite.Should().Be(inner.CanWrite);
    }
}
