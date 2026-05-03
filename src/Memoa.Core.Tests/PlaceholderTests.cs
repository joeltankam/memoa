using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture]
internal class PlaceholderTests
{
    [Test]
    public void Placeholder_ShouldPass()
    {
        var result = true;
        Assert.That(result, Is.True);
    }
}
