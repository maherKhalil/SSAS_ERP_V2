using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ReFillPrescriptions : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string FrequencyofRefills { get; set; }
        public string FrequencyofRefillsType { get; set; }
        public string NumberofRefills { get; set; }
        public int PrescriptionsDetailsId { get; set; }
        public string ContinusRefill { get; set; }
        public Guid TenantId { get; set; }
    }
}
