using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OrderCategoryMaster : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int OrderTypeID { get; set; }
        public string ArabicNameDescription { get; set; }
        public string EnglishNameDescription { get; set; }
        public string Status { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
