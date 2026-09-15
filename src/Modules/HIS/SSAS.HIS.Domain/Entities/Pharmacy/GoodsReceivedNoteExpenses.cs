using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class GoodsReceivedNoteExpenses : Entity<string>, ITenantOwnedEntity
{

    public GoodsReceivedNoteExpenses(string id) : base(id) { }
    public GoodsReceivedNoteExpenses() : base(Guid.NewGuid().ToString()) { }

    public string? ExpensesID { get; set; }
    public string? GRNId { get; set; }
    public string? Amount { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
