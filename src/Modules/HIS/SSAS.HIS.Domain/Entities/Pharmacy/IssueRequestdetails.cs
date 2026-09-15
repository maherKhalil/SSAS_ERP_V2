using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class IssueRequestdetails : Entity<string>, ITenantOwnedEntity
{

    public IssueRequestdetails(string id) : base(id) { }
    public IssueRequestdetails() : base(Guid.NewGuid().ToString()) { }

    public string? IssueRequestId { get; set; }
    public string? DrugID { get; set; }
    public string? UnitConversionID { get; set; }
    public string? Quantity { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreationDate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModificationDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
