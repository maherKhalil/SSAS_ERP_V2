using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class OrganismAntibioticSensitivity : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SectionId { get; set; }
        public int TestId { get; set; }
        public int OrganismId { get; set; }
        public int AntibioticId { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
