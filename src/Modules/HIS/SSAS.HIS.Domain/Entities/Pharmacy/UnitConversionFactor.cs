using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class UnitConversionFactor : Entity<string>, ITenantOwnedEntity
{

    public UnitConversionFactor(string id) : base(id) { }
    public UnitConversionFactor() : base(Guid.NewGuid().ToString()) { }

    public string? UnitTemplateID { get; set; }
    public string? UnitID { get; set; }
    public string? ConversionValue { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
