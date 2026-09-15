using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class AllergyDetails : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public int AllergyMasterID { get; set; }
        public Guid TenantId { get; set; }
    }
}
