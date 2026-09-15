using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PatientAllergyNew : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string AllergyType { get; set; }
        public int genericID { get; set; }
        public string Comment { get; set; }
        public int PatientId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int DrugID { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
