using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DiseaseMaster : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DiseaseCategoryID { get; set; }
        public string DiseaseName { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public bool IsChronic { get; set; }
        public bool IsCommunicable { get; set; }
        public bool ISHIbB { get; set; }
        public string DiseaseNameAr { get; set; }
        public Guid TenantId { get; set; }
    }
}
