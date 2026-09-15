using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OutResourceType : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string TypeCode { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string Status { get; set; }
        public string HospitalCase { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
