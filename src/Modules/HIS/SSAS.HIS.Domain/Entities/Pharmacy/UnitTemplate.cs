using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class UnitTemplate : Entity<string>, ITenantOwnedEntity
{

    public UnitTemplate(string id) : base(id) { }
    public UnitTemplate() : base(Guid.NewGuid().ToString()) { }

    public string? UnitTemplateCode { get; set; }
    public string? UnitTemplateNameAr { get; set; }
    public string? UnitTemplateName { get; set; }
    public string? BaseUnitID { get; set; }
    public string? SubStoreID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
