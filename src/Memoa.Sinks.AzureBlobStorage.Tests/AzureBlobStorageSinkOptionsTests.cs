using FluentAssertions;
using Memoa.Sinks.AzureBlobStorage;
using NUnit.Framework;

namespace Memoa.Sinks.AzureBlobStorage.Tests;

[TestFixture(TestOf = typeof(AzureBlobStorageSinkOptions))]
internal class AzureBlobStorageSinkOptionsTests
{
    [Test]
    public void Defaults_ShouldHaveExpectedValues()
    {
        // Arrange & Act
        var options = new AzureBlobStorageSinkOptions();

        // Assert
        options.ContainerName.Should().Be("memoa-requests");
        options.BlobPrefix.Should().BeNull();
        options.CreateContainerIfNotExists.Should().BeTrue();
        options.BlobNameFormat.Should().Be("{year}/{month}/{day}/{hour}/{id}.json");
        options.ConnectionString.Should().BeNull();
        options.ServiceUri.Should().BeNull();
    }
}
