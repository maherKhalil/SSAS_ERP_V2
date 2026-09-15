using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class HeadUpTiltTableICDCodes : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int HeadUpTiltTableId { get; set; }
        public int ICDCodeId { get; set; }
        public DateTime CreationDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string ModifiedBy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
