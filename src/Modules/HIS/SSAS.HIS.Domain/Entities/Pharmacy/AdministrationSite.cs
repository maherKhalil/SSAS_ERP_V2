using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class AdministrationSite : Entity<string>, ITenantOwnedEntity
{

    public AdministrationSite(string id) : base(id) { }
    public AdministrationSite() : base(Guid.NewGuid().ToString()) { }

    public string? Value { get; set; }
    public string? Description { get; set; }
    public Guid TenantId { get; set; }

}
