using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class FrequencyMaster : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string EnglishNameDescription { get; set; }
        public string ArabicNameDescription { get; set; }
        public string Frequency { get; set; }
        public string Type { get; set; }
        public string FrequencyType { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
