using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ScrapDetail : Entity<string>, ITenantOwnedEntity
{

    public ScrapDetail(string id) : base(id) { }
    public ScrapDetail() : base(Guid.NewGuid().ToString()) { }

    public string? ScrapHeaderId { get; set; }
    public string? DrugID { get; set; }
    public string? StockBatchId { get; set; }
    public string? Date { get; set; }
    public string? UnitConversionID { get; set; }
    public string? CurrentQty { get; set; }
    public string? ScrapQty { get; set; }
    public string? TotalReturnQty { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
