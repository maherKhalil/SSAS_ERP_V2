using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class ResultEntryDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int ResultEntryId { get; set; }
        public int InvestigationDetailsId { get; set; }
        public string OPIPNO { get; set; }
        public int DoctorId { get; set; }
        public DateTime TimeStudy { get; set; }
        public string Abnormal { get; set; }
        public string Redone { get; set; }
        public string Remarks { get; set; }
        public string Comments { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string Findings { get; set; }
        public string Conclusion { get; set; }
        public Guid TenantId { get; set; }
    }
}
