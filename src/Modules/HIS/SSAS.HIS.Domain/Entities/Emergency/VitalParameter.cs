using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class VitalParameter : Entity<string>, ITenantOwnedEntity
{

    public VitalParameter(string id) : base(id) { }
    public VitalParameter() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
