using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Estimatedmission : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public DateTime SurgeryDate { get; set; }
        public int AdmissionRequestId { get; set; }
        public string EstimateNO { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public decimal CostEstimateStatus { get; set; }
        public int PatientId { get; set; }
        public bool IsLinked { get; set; }
        public string OPnumber { get; set; }
        public int DoctorID { get; set; }
        public DateTime AdmissionDate { get; set; }
        public string Days { get; set; }
        public int RoomTypeID { get; set; }
        public string EstimatedmissionStatus { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
