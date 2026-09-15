using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DrugsWithInfusionRateDetails_StatusHistory : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DrugsWithInfusionRateDetailsID { get; set; }
        public int StatusID { get; set; }
        public string Note { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
