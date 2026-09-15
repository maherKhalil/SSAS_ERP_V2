using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class SpecialityGroupDetails : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int SpecialityMasterID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Creationdate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string NameAr { get; set; }
        public string AssessmentSpecialist { get; set; }
        public Guid TenantId { get; set; }
    }
}
