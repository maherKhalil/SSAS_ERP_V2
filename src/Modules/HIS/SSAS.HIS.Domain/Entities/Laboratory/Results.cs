using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class Results : ITenantOwnedEntity
    {
        public int ResultID { get; set; }
        public int SectionID { get; set; }
        public string ResultName { get; set; }
        public int ResultTypeId { get; set; }
        public string Units { get; set; }
        public string SIUnits { get; set; }
        public string ConversionFactor { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int TestID { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
