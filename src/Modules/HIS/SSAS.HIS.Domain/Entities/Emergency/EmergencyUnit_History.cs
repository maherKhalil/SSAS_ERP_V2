using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class EmergencyUnit_History : Entity<string>, ITenantOwnedEntity
{

    public EmergencyUnit_History(string id) : base(id) { }
    public EmergencyUnit_History() : base(Guid.NewGuid().ToString()) { }

    public string? EmergencyUnitID { get; set; }
    public string? ChangeDate { get; set; }
    public string? DoctorID { get; set; }
    public string? Reason { get; set; }
    public string? ERCode { get; set; }
    public string? ItemID { get; set; }
    public string? other { get; set; }
    public string? CompanyID { get; set; }
    public string? BranchID { get; set; }
    public Guid TenantId { get; set; }

}
