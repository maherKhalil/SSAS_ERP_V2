using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugClassification : Entity<string>, ITenantOwnedEntity
{

    public DrugClassification(string id) : base(id) { }
    public DrugClassification() : base(Guid.NewGuid().ToString()) { }

    public string? NameEn { get; set; }
    public string? NameAr { get; set; }
    public string? Code { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
