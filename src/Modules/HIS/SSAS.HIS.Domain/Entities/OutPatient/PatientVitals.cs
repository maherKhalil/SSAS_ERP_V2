using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class PatientVitals : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string OP_IP { get; set; }
        public DateTime ReadingDate { get; set; }
        public DateTime StartDate { get; set; }
        public string PeroidType { get; set; }
        public string Frequency { get; set; }
        public string MonitoredDays { get; set; }
        public DateTime StartTime { get; set; }
        public int VitalParametersDeptWiseId { get; set; }
        public int PateintId { get; set; }
        public string Value { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
