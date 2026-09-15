using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SpecimenTypes : ITenantOwnedEntity
    {
        public int SpecimenTypesID { get; set; }
        public string SpecimenTypesName { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string Apprivate { get; set; }
        public Guid TenantId { get; set; }
    }
}
