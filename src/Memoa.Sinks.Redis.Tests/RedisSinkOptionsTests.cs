using FluentAssertions;
using Memoa.Sinks.Redis;
using NUnit.Framework;

namespace Memoa.Sinks.Redis.Tests;

[TestFixture(TestOf = typeof(RedisSinkOptions))]
internal class RedisSinkOptionsTests
{
    [Test]
    public void Defaults_ShouldHaveExpectedValues()
    {
        // Act
        var options = new RedisSinkOptions();

        // Assert
        options.ConnectionString.Should().BeNull();
        options.StreamKey.Should().Be("memoa:requests");
        options.MaxLength.Should().Be(10_000);
        options.Database.Should().Be(-1);
        options.KeyPrefix.Should().BeNull();
    }

    [Test]
    public void StreamKey_CanBeChanged()
    {
        // Arrange
        var options = new RedisSinkOptions();

        // Act
        options.StreamKey = "custom:stream";

        // Assert
        options.StreamKey.Should().Be("custom:stream");
    }

    [Test]
    public void MaxLength_CanBeSetToNull_ForUnlimited()
    {
        // Arrange
        var options = new RedisSinkOptions();

        // Act
        options.MaxLength = null;

        // Assert
        options.MaxLength.Should().BeNull();
    }

    [Test]
    public void Database_CanBeChanged()
    {
        // Arrange
        var options = new RedisSinkOptions();

        // Act
        options.Database = 2;

        // Assert
        options.Database.Should().Be(2);
    }

    [Test]
    public void KeyPrefix_CanBeSet()
    {
        // Arrange
        var options = new RedisSinkOptions();

        // Act
        options.KeyPrefix = "myapp";

        // Assert
        options.KeyPrefix.Should().Be("myapp");
    }
}
