using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class BedStatusChangeTracker : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int BedLockPurposeId { get; set; }
        public int BedId { get; set; }
        public string BedStatusChangeProcessEnumValue { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
