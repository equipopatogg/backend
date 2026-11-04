using IntegrationTest;

namespace DatabaseTest;

public sealed class FunctionalFlowTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public FunctionalFlowTests(PactoggWebFactory factory) => _factory = factory;

    [Fact]
    public Task RegisterLoginLogoutAndModerateBeforeStorage_Works() =>
        FunctionalFlowChecks.AssertAuthAndModerationBeforeStorageAsync(_factory);
}
