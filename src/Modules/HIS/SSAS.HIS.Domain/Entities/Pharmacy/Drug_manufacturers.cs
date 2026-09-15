using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Drug_manufacturers : Entity<string>, ITenantOwnedEntity
{

    public Drug_manufacturers(string id) : base(id) { }
    public Drug_manufacturers() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? NameAr { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
