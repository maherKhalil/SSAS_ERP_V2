using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class Disease_InfectionTypes : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int DiseaseID { get; set; }
        public int InfectionTypeID { get; set; }
        public Guid TenantId { get; set; }
    }
}
