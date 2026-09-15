using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Frequencies : Entity<string>, ITenantOwnedEntity
{

    public Frequencies(string id) : base(id) { }
    public Frequencies() : base(Guid.NewGuid().ToString()) { }

    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? NameAr { get; set; }
    public Guid TenantId { get; set; }

}
