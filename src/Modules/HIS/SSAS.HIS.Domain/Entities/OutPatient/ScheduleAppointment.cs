using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ScheduleAppointment : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int DepartmentID { get; set; }
        public int DoctorsID { get; set; }
        public int SessionID { get; set; }
        public DateTime Time { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
