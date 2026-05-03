using FluentAssertions;
using Memoa.Sinks.AzureBlobStorage;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Memoa.Sinks.AzureBlobStorage.Tests;

[TestFixture(TestOf = typeof(AzureBlobStorageSinkExtensions))]
internal class AzureBlobStorageSinkExtensionsTests
{
    [Test]
    public void AzureBlobStorage_ShouldRegisterSinkWithConnectionString()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.AzureBlobStorage("UseDevelopmentStorage=true");

        var sp = services.BuildServiceProvider();

        // Assert
        var sinks = sp.GetServices<IRequestSink>().ToList();
        sinks.Should().ContainSingle();
        sinks[0].Should().BeOfType<AzureBlobStorageSink>();
    }

    [Test]
    public void AzureBlobStorage_ShouldApplyCustomOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.AzureBlobStorage("UseDevelopmentStorage=true", opts =>
        {
            opts.ContainerName = "custom-container";
            opts.BlobPrefix = "prefix";
        });

        var sp = services.BuildServiceProvider();

        // Assert
        var options = sp.GetRequiredService<AzureBlobStorageSinkOptions>();
        options.ContainerName.Should().Be("custom-container");
        options.BlobPrefix.Should().Be("prefix");
    }
}
