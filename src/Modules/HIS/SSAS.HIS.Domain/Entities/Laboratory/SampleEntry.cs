using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SampleEntry : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int InvestigationRequestDetailsId { get; set; }
        public int SpecimenID { get; set; }
        public int LabId { get; set; }
        public string SampleNo { get; set; }
        public string Resample { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int TechnicanID { get; set; }
        public int RejectionReasonId { get; set; }
        public string Other { get; set; }
        public string CancelReason { get; set; }
        public string CancelSample { get; set; }
        public bool IsTakeSampleOutSideHospital { get; set; }
        public int BranchId { get; set; }
        public string OutSourceLab { get; set; }
        public Guid TenantId { get; set; }
    }
}
