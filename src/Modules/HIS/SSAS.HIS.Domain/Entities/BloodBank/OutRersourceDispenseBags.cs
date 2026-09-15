using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class OutRersourceDispenseBags : Entity<string>, ITenantOwnedEntity
{

    public OutRersourceDispenseBags(string id) : base(id) { }
    public OutRersourceDispenseBags() : base(Guid.NewGuid().ToString()) { }

    public string? OutResourceID { get; set; }
    public string? BagID { get; set; }
    public string? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CompanyID { get; set; }
    public string? DispenseDate { get; set; }
    public string? BagCode { get; set; }
    public string? ServiceID { get; set; }
    public string? Amount { get; set; }
    public string? Paid { get; set; }
    public string? EntryCodes { get; set; }
    public Guid TenantId { get; set; }

}
