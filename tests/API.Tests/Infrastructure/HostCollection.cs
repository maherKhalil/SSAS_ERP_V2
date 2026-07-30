namespace SSAS.API.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HostIntegrationTestGroup : ICollectionFixture<HostWebApplicationFactory>
{
  public const string Name = "Host integration tests";
}
