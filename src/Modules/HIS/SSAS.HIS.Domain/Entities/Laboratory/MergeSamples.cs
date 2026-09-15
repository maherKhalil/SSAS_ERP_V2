using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class MergeSamples : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SampleNumberId { get; set; }
        public int MergedWithSampleId { get; set; }
        public string NewSampleNumber { get; set; }
        public DateTime CreateDate { get; set; }
        public string CreatedBy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
