using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PatientDiet : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DietId { get; set; }
        public string PNumber { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
