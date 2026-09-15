using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Brands : Entity<string>, ITenantOwnedEntity
{

    public Brands(string id) : base(id) { }
    public Brands() : base(Guid.NewGuid().ToString()) { }

    public string? BrandName { get; set; }
    public string? GenericNamesId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
