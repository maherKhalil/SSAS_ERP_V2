using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class VaccinationDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int VaccinationID { get; set; }
        public string DosageNo { get; set; }
        public string MinMonth { get; set; }
        public string MaxMonth { get; set; }
        public int ServiceID { get; set; }
        public string Rate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
