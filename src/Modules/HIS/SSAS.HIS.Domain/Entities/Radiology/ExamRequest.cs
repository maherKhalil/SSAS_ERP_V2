using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class ExamRequest : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public string IPNumber { get; set; }
        public string AccessionNo { get; set; }
        public DateTime ExamDate { get; set; }
        public int DeviceID { get; set; }
        public string Status { get; set; }
        public int ExamID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int UserID { get; set; }
        public int DoctorID { get; set; }
        public string Notes { get; set; }
        public int FlagsID { get; set; }
        public DateTime ReservedDateFrom { get; set; }
        public DateTime ReservedDateTo { get; set; }
        public DateTime ReservedTimeFrom { get; set; }
        public DateTime ReservedTimeTo { get; set; }
        public DateTime TimeStartExam { get; set; }
        public DateTime TimeEndExam { get; set; }
        public string ReportNotes { get; set; }
        public int ServiceId { get; set; }
        public DateTime RequestDate { get; set; }
        public string RequestNumber { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
