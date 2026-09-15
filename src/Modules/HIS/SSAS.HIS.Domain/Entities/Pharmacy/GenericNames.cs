using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class GenericNames : Entity<string>, ITenantOwnedEntity
{

    public GenericNames(string id) : base(id) { }
    public GenericNames() : base(Guid.NewGuid().ToString()) { }

    public string? GenericNamesAr { get; set; }
    public string? GenericNamesValue { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
