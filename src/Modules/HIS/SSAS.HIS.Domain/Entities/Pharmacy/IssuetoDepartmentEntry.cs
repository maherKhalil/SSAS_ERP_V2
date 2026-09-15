using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class IssuetoDepartmentEntry : Entity<string>, ITenantOwnedEntity
{

    public IssuetoDepartmentEntry(string id) : base(id) { }
    public IssuetoDepartmentEntry() : base(Guid.NewGuid().ToString()) { }

    public string? IssuetoDepartmentId { get; set; }
    public string? DrugID { get; set; }
    public string? StockBatchId { get; set; }
    public string? Date { get; set; }
    public string? UnitConversionID { get; set; }
    public string? CurrentQty { get; set; }
    public string? IssueQty { get; set; }
    public string? TotalReturnQty { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
