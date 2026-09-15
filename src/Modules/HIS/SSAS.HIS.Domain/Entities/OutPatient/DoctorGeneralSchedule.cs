using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DoctorGeneralSchedule : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int DayID { get; set; }
        public int SubSpecialityID { get; set; }
        public int DoctorID { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public int SessionId { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
