using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class ExternalAgencies : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int HospitalId { get; set; }
        public int PatientId { get; set; }
        public int CustomerId { get; set; }
        public int NationalityId { get; set; }
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
