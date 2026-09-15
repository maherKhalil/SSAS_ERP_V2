using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ScrapHeader : Entity<string>, ITenantOwnedEntity
{

    public ScrapHeader(string id) : base(id) { }
    public ScrapHeader() : base(Guid.NewGuid().ToString()) { }

    public string? MainStockID { get; set; }
    public string? ScrapNumber { get; set; }
    public string? ScrapDate { get; set; }
    public string? StockID { get; set; }
    public string? Remarks { get; set; }
    public string? Status { get; set; }
    public string? IssueRequestID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? EntryCodes { get; set; }
    public Guid TenantId { get; set; }

}
