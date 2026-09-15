using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class PatientAllergyUpdates : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public bool IsAllergy { get; set; }
        public string AllergyDesc { get; set; }
        public string Category { get; set; }
        public int PatientID { get; set; }
        public string CreatorName { get; set; }
        public DateTime CreateDate { get; set; }
        public int AllergyDetailsID { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
