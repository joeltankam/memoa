using Amazon.S3;
using FluentAssertions;
using Memoa.Sinks.AmazonS3;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace Memoa.Sinks.AmazonS3.Tests;

[TestFixture(TestOf = typeof(AmazonS3SinkExtensions))]
internal class AmazonS3SinkExtensionsTests
{
    [Test]
    public void AmazonS3_ShouldRegisterSinkAsRequestSink()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Pre-register mock so TryAddSingleton won't try to create a real AmazonS3Client
        services.AddSingleton<IAmazonS3>(Mock.Of<IAmazonS3>());

        // Act
        builder.WriteTo.AmazonS3(opts =>
        {
            opts.BucketName = "test-bucket";
        });

        var sp = services.BuildServiceProvider();
        var sinks = sp.GetServices<IRequestSink>().ToList();

        // Assert
        sinks.Should().ContainSingle();
        sinks[0].Should().BeOfType<AmazonS3Sink>();
    }

    [Test]
    public void AmazonS3_ShouldApplyConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAmazonS3>(Mock.Of<IAmazonS3>());
        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.AmazonS3(opts =>
        {
            opts.BucketName = "my-bucket";
            opts.KeyPrefix = "prefix";
            opts.CreateBucketIfNotExists = false;
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<AmazonS3SinkOptions>();

        // Assert
        options.BucketName.Should().Be("my-bucket");
        options.KeyPrefix.Should().Be("prefix");
        options.CreateBucketIfNotExists.Should().BeFalse();
    }

    [Test]
    public void AmazonS3_ShouldNotReplaceExistingIAmazonS3Registration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var existingMock = new Mock<IAmazonS3>(MockBehavior.Strict);
        services.AddSingleton(existingMock.Object);

        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.AmazonS3(opts => opts.BucketName = "bucket");

        var sp = services.BuildServiceProvider();
        var resolvedS3 = sp.GetRequiredService<IAmazonS3>();

        // Assert — TryAddSingleton should not replace the pre-registered one
        resolvedS3.Should().BeSameAs(existingMock.Object);
    }

    [Test]
    public void AmazonS3_ShouldReturnSinkBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAmazonS3>(Mock.Of<IAmazonS3>());
        var builder = services.AddMemoa();

        // Act
        var result = builder.WriteTo.AmazonS3(opts => opts.BucketName = "bucket");

        // Assert
        result.Should().BeSameAs(builder.WriteTo);
    }
}
