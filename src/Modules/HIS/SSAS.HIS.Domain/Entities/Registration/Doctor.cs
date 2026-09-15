using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Registration;

public class Doctor : Entity<Guid>, ITenantOwnedEntity
{
    public Doctor(Guid id) : base(id) { }

    private Doctor() : base(Guid.Empty) { } // for EF Core

    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = default!;
}
