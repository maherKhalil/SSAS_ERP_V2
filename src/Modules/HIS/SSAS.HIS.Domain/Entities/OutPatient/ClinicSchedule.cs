using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ClinicSchedule : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int DayID { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int SubSpecialityID { get; set; }
        public int DoctorID { get; set; }
        public string Status { get; set; }
        public int ClinicID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Creationdate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public int SessionId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
