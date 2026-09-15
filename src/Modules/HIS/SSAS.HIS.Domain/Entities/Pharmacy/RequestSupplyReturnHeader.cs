using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class RequestSupplyReturnHeader : Entity<string>, ITenantOwnedEntity
{

    public RequestSupplyReturnHeader(string id) : base(id) { }
    public RequestSupplyReturnHeader() : base(Guid.NewGuid().ToString()) { }

    public string? RequestSupplyID { get; set; }
    public string? RequestSupplyRetuenCode { get; set; }
    public string? ReturnDate { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? EntryCodes { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
