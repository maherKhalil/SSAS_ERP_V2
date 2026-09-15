using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SamplesTransfere : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int FromLabId { get; set; }
        public int ToLabId { get; set; }
        public string SampleNumber { get; set; }
        public int SamplesReceivedId { get; set; }
        public int TechnicanId { get; set; }
        public int RecievedTechnicanId { get; set; }
        public Guid TenantId { get; set; }
    }
}
