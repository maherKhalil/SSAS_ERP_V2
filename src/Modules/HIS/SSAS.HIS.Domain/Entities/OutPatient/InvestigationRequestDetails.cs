using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class InvestigationRequestDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int InvestigationRequestId { get; set; }
        public string QTY { get; set; }
        public decimal Amount { get; set; }
        public string Priority { get; set; }
        public int ServiceId { get; set; }
        public bool IsApproved { get; set; }
        public int ApprovalId { get; set; }
        public bool Isoverride { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int PatientOrderDetailsID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int StartNurseID { get; set; }
        public int EndNurseID { get; set; }
        public string Done { get; set; }
        public string Cancelled { get; set; }
        public string RayLocation { get; set; }
        public bool IsNoDeposit { get; set; }
        public string PaymentType { get; set; }
        public string Status { get; set; }
        public string CancelledBy { get; set; }
        public string CancelledReason { get; set; }
        public string Comments { get; set; }
        public int BranchId { get; set; }
        public int insuranceId { get; set; }
        public int prescDtlID { get; set; }
        public string toothNo { get; set; }
        public string opticaltype { get; set; }
        public Guid TenantId { get; set; }
    }
}
