using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class SponsorshipCondition : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SponsorID { get; set; }
        public int CategoryID { get; set; }
        public int CarrierID { get; set; }
        public string CoverageLimit { get; set; }
        public string PerEpisode { get; set; }
        public string PerAnnum { get; set; }
        public string InpatientTreatment { get; set; }
        public string OutpatientTreatment { get; set; }
        public string ContractNumber { get; set; }
        public DateTime ContractDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime EffectiveToDate { get; set; }
        public decimal Co_paymentamount { get; set; }
        public bool IsPercentage { get; set; }
        public bool IsAmount { get; set; }
        public bool IsNet { get; set; }
        public bool IsGross { get; set; }
        public bool IsBeforeDeductible { get; set; }
        public bool IsAfterDeductible { get; set; }
        public bool IsVerifyPolicyNumber { get; set; }
        public bool IsAutoGenratePolicyNumber { get; set; }
        public bool IsImmediatesettlement { get; set; }
        public bool IsCreditsettlement { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
