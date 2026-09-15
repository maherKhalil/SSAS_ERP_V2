using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RequestProceduresHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string Type { get; set; }
        public int DoctorID { get; set; }
        public decimal TotalAmount { get; set; }
        public string OP_IPNumber { get; set; }
        public bool IsPregnant { get; set; }
        public string PregnantWeeks { get; set; }
        public string ClinicalDetails { get; set; }
        public DateTime ReqDate { get; set; }
        public string ReqStatus { get; set; }
        public string Code { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int SurgeryID { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
