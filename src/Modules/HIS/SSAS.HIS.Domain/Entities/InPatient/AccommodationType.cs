using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class AccommodationType : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string TypeCode { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string Status { get; set; }
        public string HospitalCase { get; set; }
        public int ServiceID { get; set; }
        public int AccommodationTypeID { get; set; }
        public string Companion { get; set; }
        public int BranchId { get; set; }
        public bool IsEmergency { get; set; }
        public string NameKa { get; set; }
        public Guid TenantId { get; set; }
    }
}
