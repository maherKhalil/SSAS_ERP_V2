using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Registration;

public class Specialty : Entity<Guid>, ITenantOwnedEntity
{
    public Specialty(Guid id) : base(id) { }

    private Specialty() : base(Guid.Empty) { } // for EF Core

    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
}
