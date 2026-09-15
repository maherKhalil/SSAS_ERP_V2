using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SpecimenTypeAssociation : ITenantOwnedEntity
    {
        public int SpecimenTypeAssociationID { get; set; }
        public int SpecimenTypesID { get; set; }
        public int SectionID { get; set; }
        public int TestID { get; set; }
        public int SampleTypeId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
