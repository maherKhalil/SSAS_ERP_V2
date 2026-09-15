using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class GitImages : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int MedicalObservation_GitID { get; set; }
        public int TapID { get; set; }
        public string ImageName { get; set; }
        public Guid TenantId { get; set; }
    }
}
