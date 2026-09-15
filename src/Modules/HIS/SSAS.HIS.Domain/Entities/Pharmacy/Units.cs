using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Units : Entity<string>, ITenantOwnedEntity
{

    public Units(string id) : base(id) { }
    public Units() : base(Guid.NewGuid().ToString()) { }

    public string? UnitNameArabic { get; set; }
    public string? UnitName { get; set; }
    public string? SubUnitNo { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Code { get; set; }
    public Guid TenantId { get; set; }

}
