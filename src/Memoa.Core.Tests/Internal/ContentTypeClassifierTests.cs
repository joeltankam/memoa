using FluentAssertions;
using Memoa.Internal;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(ContentTypeClassifier))]
internal class ContentTypeClassifierTests
{
    [TestCase("image/png", true)]
    [TestCase("image/jpeg", true)]
    [TestCase("video/mp4", true)]
    [TestCase("audio/mpeg", true)]
    [TestCase("application/pdf", true)]
    [TestCase("application/octet-stream", true)]
    [TestCase("application/json", false)]
    [TestCase("text/plain", false)]
    [TestCase("text/html", false)]
    public void IsBinary_ShouldClassifyCorrectly(string contentType, bool expected)
    {
        // Arrange
        var options = new MemoaCaptureOptions();
        var classifier = new ContentTypeClassifier(options.BinaryContentTypePatterns);

        // Act
        var result = classifier.IsBinary(contentType);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void IsBinary_ShouldReturnFalse_WhenContentTypeIsNull()
    {
        // Arrange
        var classifier = new ContentTypeClassifier(["image/*"]);

        // Act
        var result = classifier.IsBinary(null);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsBinary_ShouldReturnFalse_WhenContentTypeIsEmpty()
    {
        // Arrange
        var classifier = new ContentTypeClassifier(["image/*"]);

        // Act
        var result = classifier.IsBinary(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsBinary_ShouldStripParameters()
    {
        // Arrange
        var classifier = new ContentTypeClassifier(["image/*"]);

        // Act
        var result = classifier.IsBinary("image/png; charset=utf-8");

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsBinary_ShouldReturnFalse_WhenNoPatternsConfigured()
    {
        // Arrange
        var classifier = new ContentTypeClassifier([]);

        // Act
        var result = classifier.IsBinary("image/png");

        // Assert
        result.Should().BeFalse();
    }
}
