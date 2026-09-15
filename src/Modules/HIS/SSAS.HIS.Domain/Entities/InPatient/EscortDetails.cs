using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class EscortDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int EscortId { get; set; }
        public string IPNumber { get; set; }
        public string EscortName { get; set; }
        public string IdentityType { get; set; }
        public int EscortNationalId { get; set; }
        public int GenderTypeId { get; set; }
        public string EscortAge { get; set; }
        public int RelativeId { get; set; }
        public string EscortNeedBed { get; set; }
        public int BedId { get; set; }
        public bool IsActive { get; set; }
        public string Payment { get; set; }
        public string ApprovalOnTerms { get; set; }
        public string ApprovalOnTermsFileUrl { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
