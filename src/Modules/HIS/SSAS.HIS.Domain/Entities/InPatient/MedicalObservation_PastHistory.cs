using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservation_PastHistory : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string POA { get; set; }
        public string No { get; set; }
        public string Mode { get; set; }
        public string Sex { get; set; }
        public string Weight { get; set; }
        public string Age { get; set; }
        public string Remarks { get; set; }
        public int MedicalObservationobsid { get; set; }
        public Guid TenantId { get; set; }
    }
}
