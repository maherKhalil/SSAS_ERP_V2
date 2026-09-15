using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class LocationDefination : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string LatinName { get; set; }
        public string LocalName { get; set; }
        public bool ISActive { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string NumberOFPatient { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
