using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class OnCallDoctors : Entity<string>, ITenantOwnedEntity
{

    public OnCallDoctors(string id) : base(id) { }
    public OnCallDoctors() : base(Guid.NewGuid().ToString()) { }

    public string? DoctorId { get; set; }
    public string? Status { get; set; }
    public string? SpicialityID { get; set; }
    public string? CallingDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreationDate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModificationDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
