using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CancelTypesMaster : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string EnglishDescription { get; set; }
        public string ArabicDescription { get; set; }
        public string CancelType { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
