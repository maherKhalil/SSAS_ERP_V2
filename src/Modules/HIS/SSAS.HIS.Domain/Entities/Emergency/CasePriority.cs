using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class CasePriority : Entity<string>, ITenantOwnedEntity
{

    public CasePriority(string id) : base(id) { }
    public CasePriority() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
