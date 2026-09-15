using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class VaccinationSchedule : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public DateTime ScheduleDate { get; set; }
        public int vaccinationID { get; set; }
        public int FrequencyID { get; set; }
        public DateTime StartDate { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
