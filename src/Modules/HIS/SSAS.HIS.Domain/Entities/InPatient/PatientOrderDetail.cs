using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PatientOrderDetail : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientOrderMasterId { get; set; }
        public int ServiceId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string Status { get; set; }
        public string Qty { get; set; }
        public int DrugID { get; set; }
        public int GenericID { get; set; }
        public int DoseUnitID { get; set; }
        public string DrugForm { get; set; }
        public string Strength { get; set; }
        public int PrescriptionsDetailsID { get; set; }
        public int OrderTypeId { get; set; }
        public string Location { get; set; }
        public int FrequencyID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public int VerificationFromUserId { get; set; }
        public int VerificationToNurseId { get; set; }
        public DateTime VerificationDateFrom { get; set; }
        public DateTime VerificationDateTo { get; set; }
        public bool isVerified { get; set; }
        public string ExecutionBy { get; set; }
        public bool IsPaused { get; set; }
        public bool IsSkipped { get; set; }
        public int SkipInstructionID { get; set; }
        public string SkipReason { get; set; }
        public DateTime SkipDate { get; set; }
        public int InsuranceId { get; set; }
        public int RequestSuppliesDetailID { get; set; }
        public decimal Amount { get; set; }
        public string ServiceGroupName { get; set; }
        public string Comments { get; set; }
        public Guid TenantId { get; set; }
    }
}
