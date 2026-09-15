using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class GRN_LPOs : Entity<string>, ITenantOwnedEntity
{

    public GRN_LPOs(string id) : base(id) { }
    public GRN_LPOs() : base(Guid.NewGuid().ToString()) { }

    public string? GRNID { get; set; }
    public string? LPOID { get; set; }
    public Guid TenantId { get; set; }

}
