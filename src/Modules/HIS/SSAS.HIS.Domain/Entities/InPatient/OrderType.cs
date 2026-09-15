using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OrderType : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string ArabicNameDescription { get; set; }
        public string EnglishNameDescription { get; set; }
        public string TypeClass { get; set; }
        public string OrderSheetType { get; set; }
        public int InventoryID { get; set; }
        public string ExpirationDuration { get; set; }
        public string Status { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
