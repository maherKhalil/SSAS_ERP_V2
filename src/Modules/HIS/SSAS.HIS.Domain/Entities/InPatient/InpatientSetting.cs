using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class InpatientSetting : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int AdmitServiceID { get; set; }
        public string AdmitPaymentWaitingHours { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string ReDepositBalance { get; set; }
        public string InitialDeposit { get; set; }
        public int EmployeeId { get; set; }
        public DateTime OperationTimeslots { get; set; }
        public string paybeforetakenaservice { get; set; }
        public string PayDeposit { get; set; }
        public string Deposit { get; set; }
        public string Min { get; set; }
        public string Durationindays { get; set; }
        public string NumberofFlowUp { get; set; }
        public string DepositPercentage { get; set; }
        public int ConsultationServiceID { get; set; }
        public string ShowSurgicalProceduralConsentForm { get; set; }
        public int refrealServiceID { get; set; }
        public Guid TenantId { get; set; }
    }
}
