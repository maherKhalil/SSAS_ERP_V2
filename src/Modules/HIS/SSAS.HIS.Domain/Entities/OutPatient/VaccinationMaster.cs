using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class VaccinationMaster : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string VaccinationName { get; set; }
        public string NODoses { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public DateTime VaccinationTime { get; set; }
        public int CompanyID { get; set; }
        public bool ISHIbB { get; set; }
        public string VaccinationNameAr { get; set; }
        public Guid TenantId { get; set; }
    }
}
