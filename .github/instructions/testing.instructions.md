---
description: 'Testing conventions for NUnit, Moq, and FluentAssertions'
applyTo: '**/*Tests.cs'
---

# Testing Conventions

## Project Setup

- Test projects follow the naming convention `<ProjectName>.Tests` and share the same root directory as the production project.
- Test projects reference NUnit 4, Moq 4, and FluentAssertions 7 (provided automatically by `Directory.Build.targets`).
- `InternalsVisibleTo` entries are auto-generated — do not add them manually.

## Test Structure

- Apply `[TestFixture(TestOf = typeof(<TestedClass>))]` to test classes.
- Test classes are `internal`: `internal class <TestedClass>Tests`.
- Use `[Test]` attribute for test methods.
- Follow AAA (Arrange-Act-Assert) pattern with clear comments for each section.
- Name test methods: `<TestedMethod>_Should<ExpectedBehavior>_When<Condition>()` — the `_When` suffix is optional when obvious.
- Use `[SetUp]` and `[TearDown]` for per-test setup and teardown.
- Use `[OneTimeSetUp]` and `[OneTimeTearDown]` for per-class setup and teardown.
- Use `[SetUpFixture]` for assembly-level setup and teardown.
- Call `Dispose()` in `[TearDown]` for objects implementing `IDisposable`.
- Create helper methods for common test arrangements to reduce duplication and improve readability.
- Create test data builders for complex objects to simplify test setup and improve readability.
- Keep each test focused on a single behavior; avoid testing multiple behaviors in one method.
- Make tests independent and idempotent — they can run in any order with no interdependencies.
- Group related tests together in the same class; do not use `#region` directives.

## Data-Driven Tests

- Use parameterized tests to cover multiple scenarios with the same logic, reducing duplication.
- Prioritize `[TestCase]` or `[Values]` instead of creating multiple test methods.
- Use `[TestCase]` for inline test data.
- Use `[TestCaseSource]` for programmatically generated data.
- Use `[Values]` for simple parameter combinations.
- Use `[ValueSource]` for property or method-based data sources.
- Use `[Random]` for random numeric test values.
- Use `[Range]` for sequential numeric test values.
- Use `[Combinatorial]` or `[Pairwise]` for combining multiple parameters.

## Assertions

- Prefer FluentAssertions over NUnit assertions (`.Should().Be()`, `.Should().BeEquivalentTo()`).
- Use `Assert.That` with constraint model for NUnit-specific scenarios (`Is.EqualTo`, `Is.SameAs`, `Contains.Item`).
- Use `CollectionAssert` for collection comparisons when not using FluentAssertions.
- Use `StringAssert` for string-specific assertions when not using FluentAssertions.
- Use descriptive failure messages for non-obvious assertions.
- Create custom assertion methods for complex assertions to improve readability and maintainability.

## Mocking

- Mock dependencies to isolate units under test; use interfaces to facilitate mocking.
- Use `MockBehavior.Strict` by default.
- Use `Mock.Of<T>(MockBehavior.Strict)` when no setup is needed.
- Suffix mock variables with `Mock` (e.g., `repositoryMock`).
- Always call `VerifyAll()` on all mocks after act.
- Prefer `It.Is<>()` with a predicate over `It.IsAny<>()`.
- Mock only the behavior needed for the test case to keep tests focused and maintainable.
- Do not mock loggers — use `NullLogger<T>.Instance`.
- Use real `CancellationToken`: `new CancellationTokenSource().Token`.
- Avoid mock fields — create mocks within test methods or helpers for isolation.

Example:

```csharp
[Test]
public async Task ProcessData_ShouldCallRepository_WhenValidData()
{
    // Arrange
    var data = new Data { Id = 1, Value = "test" };
    var cancellationToken = new CancellationTokenSource().Token;

    var repositoryMock = new Mock<IRepository>(MockBehavior.Strict);
    repositoryMock
        .Setup(r => r.SaveAsync(It.Is<Data>(d => d.Id == 1 && d.Value == "test"), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true)
        .Verifiable();

    var service = new DataService(repositoryMock.Object, NullLogger<DataService>.Instance);

    // Act
    var result = await service.ProcessDataAsync(data, cancellationToken).ConfigureAwait(false);

    // Assert
    result.Should().BeTrue();
    repositoryMock.VerifyAll();
}
```

## Test Organization

- Group tests by feature or component.
- Use test categories to group related tests and facilitate selective test execution.
- Use `[Order]` to control test execution order when necessary.
- Use `[Description]` to provide additional test information.
- Consider `[Explicit]` for tests that shouldn't run automatically.
- Use `[Ignore("Reason")]` to temporarily skip tests.

## Integration Tests

- Place in a separate `Integration/` folder within the test project.
- Mark with `[Category("Integration")]`.
- Name: `<TestedClass>IntegrationTests` (internal class).
- Use `ServiceCollection` and `ServiceProvider` to set up a test-specific DI container.
- Use `WebApplicationFactory<TEntryPoint>` for testing ASP.NET Core applications.
- Use `TestServer` for testing HTTP clients and APIs.
- Use `TestRdwsDataDbContext` for tests that require database interactions.
- Use `[Category("Database")]` for DB tests.
- Use Azurite and `AzureOptions.Local` for tests that require Azure Storage interactions.
- Use `[Category("Azurite")]` for Azure Storage tests.
- Prefer actual implementations over mocks; limit mocking to external boundaries.
- Use realistic test data and scenarios to better simulate real-world usage.

## Async

- Mark async tests with `async Task`.
- Always use `.ConfigureAwait(false)` when awaiting.

## Coding Conventions

- Use `.editorconfig` at the root of the repository to enforce consistent coding styles.
- Avoid abbreviations in variable names, but keep names short when possible.
- Use descriptive variable names that clearly express intent.
- Follow consistent naming patterns throughout tests.

## Canonical Test Data

```csharp
var identifier = new BondIdentifier("581G90");
var metadata = new InternalMetadata(
    DateOnly.FromDateTime(DateTime.Now),
    DateTimeOffset.UtcNow,
    RdwsDataset.Test,
    null);
var dataUri = new OneIdDataUri(MarketBondPath.BondCleanPrices, identifier);
var computeData = new PointMarketDataValue<double>(0.0001d);
var transportData = new TransportData<ComputeData>(dataUri, computeData);
```
