using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SamplesReceivedDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SamplesReceivedId { get; set; }
        public string ExtSampleNo { get; set; }
        public int TestId { get; set; }
        public string SampleNo { get; set; }
        public DateTime CollectedDate { get; set; }
        public DateTime CollectedTime { get; set; }
        public string PatientName { get; set; }
        public int SexId { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int InvestigationDetailID { get; set; }
        public bool IsCollected { get; set; }
        public string Resample { get; set; }
        public string LabRequestStatus { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
