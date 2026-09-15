using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodStream : Entity<string>, ITenantOwnedEntity
{

    public BloodStream(string id) : base(id) { }
    public BloodStream() : base(Guid.NewGuid().ToString()) { }

    public string? CreatedBy { get; set; }
    public string? CreationDate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModificationDate { get; set; }
    public string? NurseID { get; set; }
    public string? DoctorID { get; set; }
    public string? PatientId { get; set; }
    public string? Cathetertype { get; set; }
    public string? PerformhandBloodStream { get; set; }
    public string? Usebetadine { get; set; }
    public string? Maximalsterile { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
