using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class LocalPurchaseOrderExpenses : Entity<string>, ITenantOwnedEntity
{

    public LocalPurchaseOrderExpenses(string id) : base(id) { }
    public LocalPurchaseOrderExpenses() : base(Guid.NewGuid().ToString()) { }

    public string? ExpensesID { get; set; }
    public string? LPOID { get; set; }
    public string? Amount { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
