using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class EmergencySchedule : Entity<string>, ITenantOwnedEntity
{

    public EmergencySchedule(string id) : base(id) { }
    public EmergencySchedule() : base(Guid.NewGuid().ToString()) { }

    public string? DayID { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? SubSpecialityID { get; set; }
    public string? DoctorID { get; set; }
    public string? Status { get; set; }
    public string? CreatedBy { get; set; }
    public string? Creationdate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModificationDate { get; set; }
    public string? SessionId { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
