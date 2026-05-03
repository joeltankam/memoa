using FluentAssertions;
using Memoa.Sinks.AmazonS3;
using NUnit.Framework;

namespace Memoa.Sinks.AmazonS3.Tests;

[TestFixture(TestOf = typeof(AmazonS3SinkOptions))]
internal class AmazonS3SinkOptionsTests
{
    [Test]
    public void Defaults_ShouldHaveExpectedValues()
    {
        // Act
        var options = new AmazonS3SinkOptions();

        // Assert
        options.BucketName.Should().Be("memoa-requests");
        options.KeyPrefix.Should().BeNull();
        options.KeyFormat.Should().Be("{year}/{month}/{day}/{hour}/{id}.json");
        options.Region.Should().BeNull();
        options.ServiceUrl.Should().BeNull();
        options.ForcePathStyle.Should().BeFalse();
        options.CreateBucketIfNotExists.Should().BeTrue();
    }

    [Test]
    public void BucketName_CanBeChanged()
    {
        // Arrange
        var options = new AmazonS3SinkOptions();

        // Act
        options.BucketName = "custom-bucket";

        // Assert
        options.BucketName.Should().Be("custom-bucket");
    }

    [Test]
    public void KeyPrefix_CanBeSet()
    {
        // Arrange
        var options = new AmazonS3SinkOptions();

        // Act
        options.KeyPrefix = "prefix/path";

        // Assert
        options.KeyPrefix.Should().Be("prefix/path");
    }

    [Test]
    public void CreateBucketIfNotExists_CanBeDisabled()
    {
        // Arrange
        var options = new AmazonS3SinkOptions();

        // Act
        options.CreateBucketIfNotExists = false;

        // Assert
        options.CreateBucketIfNotExists.Should().BeFalse();
    }
}
