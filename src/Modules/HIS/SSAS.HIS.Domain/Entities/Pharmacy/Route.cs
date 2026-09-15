using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Route : Entity<string>, ITenantOwnedEntity
{

    public Route(string id) : base(id) { }
    public Route() : base(Guid.NewGuid().ToString()) { }

    public string? Value { get; set; }
    public string? Description { get; set; }
    public Guid TenantId { get; set; }

}
