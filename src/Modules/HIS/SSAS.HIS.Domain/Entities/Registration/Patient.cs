using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Registration;

public class Patient : Entity<Guid>, ITenantOwnedEntity
{
    public Patient(Guid id) : base(id) { }

    private Patient() : base(Guid.Empty) { } // for EF Core

    public Guid TenantId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = default!;
}
