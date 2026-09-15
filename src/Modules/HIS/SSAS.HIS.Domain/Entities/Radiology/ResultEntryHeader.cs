using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class ResultEntryHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int BrabchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
