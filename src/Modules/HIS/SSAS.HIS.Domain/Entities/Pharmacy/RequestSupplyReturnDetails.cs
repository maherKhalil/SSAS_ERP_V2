using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class RequestSupplyReturnDetails : Entity<string>, ITenantOwnedEntity
{

    public RequestSupplyReturnDetails(string id) : base(id) { }
    public RequestSupplyReturnDetails() : base(Guid.NewGuid().ToString()) { }

    public string? RequestSupplyReturnHeaderID { get; set; }
    public string? RequestSupplyDetailsID { get; set; }
    public string? ReturnedQty { get; set; }
    public string? UnitConversionID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? PateintPaidAmount { get; set; }
    public string? SponserPaidAmount { get; set; }
    public string? SponserId { get; set; }
    public Guid TenantId { get; set; }

}
