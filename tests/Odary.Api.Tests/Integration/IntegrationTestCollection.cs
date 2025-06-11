using Xunit;

namespace Odary.Api.Tests.Integration;

/// <summary>
/// This collection definition ensures that integration tests that share the TestWebApplicationFactory
/// don't run in parallel, preventing database conflicts and resource contention.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
} 