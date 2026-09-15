using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OrdersEntery : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public int OrderTypeID { get; set; }
        public int OrderCategoryID { get; set; }
        public int OrderItemID { get; set; }
        public string Quantity { get; set; }
        public int FrequencyMasterID { get; set; }
        public string Eurgent { get; set; }
        public string Comment { get; set; }
        public string Status { get; set; }
        public string DurationNumber { get; set; }
        public string DurationType { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
