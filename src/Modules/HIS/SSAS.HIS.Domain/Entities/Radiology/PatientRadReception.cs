using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class PatientRadReception : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public int TechnicianID { get; set; }
        public int DeviceID { get; set; }
        public int ReceptionID { get; set; }
        public int PatientID { get; set; }
        public string PatientOPIP { get; set; }
        public DateTime RadStartDate { get; set; }
        public string status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int InvestigationRequestDetailsID { get; set; }
        public string Approve { get; set; }
        public string ChangeReason { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
