using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DrugsWithInfusionRateDetailsDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DrugsWithInfusionRateDetailsID { get; set; }
        public int DrugID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int GenericID { get; set; }
        public decimal TotalDose { get; set; }
        public string Volume { get; set; }
        public int TemplateId { get; set; }
        public Guid TenantId { get; set; }
    }
}
