using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class ScheduleSurgery : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SurgeonId { get; set; }
        public int AnesthetistId { get; set; }
        public int AssistantId { get; set; }
        public int ReqId { get; set; }
        public int PatientId { get; set; }
        public string ScheduleSurgeryNO { get; set; }
        public string ScheduleSurgeryStatus { get; set; }
        public int OperationTheatreId { get; set; }
        public int SurgeryId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime FromTime { get; set; }
        public DateTime ToTime { get; set; }
        public string AdditionalDetailsForScheduledSurgeries { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
