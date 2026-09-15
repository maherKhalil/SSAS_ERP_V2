using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Buildings : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string BuildingCode { get; set; }
        public string Description { get; set; }
        public string NameEn { get; set; }
        public int BuildingStatusId { get; set; }
        public string BuildingSequence { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
