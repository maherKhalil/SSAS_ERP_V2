using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class SupplierType : ITenantOwnedEntity
{

    public string? SupplierTypeID { get; set; }
    public string? SupplierTypeNameEN { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? SupplierTypeNameAR { get; set; }
    public Guid TenantId { get; set; }

}
