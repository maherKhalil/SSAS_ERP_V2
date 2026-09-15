using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Blacklist : ITenantOwnedEntity
    {
        public int BlacklistId { get; set; }
        public string NationalIdType { get; set; }
        public string Reason { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string IdentificationNo { get; set; }
        public Guid TenantId { get; set; }
    }
}
