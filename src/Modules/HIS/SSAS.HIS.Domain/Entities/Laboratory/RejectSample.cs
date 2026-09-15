using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class RejectSample : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int RejectionReasonId { get; set; }
        public string Other { get; set; }
        public int SampleEntryId { get; set; }
        public Guid TenantId { get; set; }
    }
}
