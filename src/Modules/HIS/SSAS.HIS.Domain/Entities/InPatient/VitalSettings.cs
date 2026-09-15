using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class VitalSettings : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Category { get; set; }
        public string AgeFrom { get; set; }
        public string AgeTo { get; set; }
        public string PulseFrom { get; set; }
        public string PulseTo { get; set; }
        public string RespRateFrom { get; set; }
        public string RespRateTo { get; set; }
        public string SystolicBPFrom { get; set; }
        public string SystolicBPTo { get; set; }
        public string TempratureFrom { get; set; }
        public string TempratureTo { get; set; }
        public string Notes { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string NameKa { get; set; }
        public Guid TenantId { get; set; }
    }
}
