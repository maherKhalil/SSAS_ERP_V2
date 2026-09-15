using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CafeteriaCharges : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string LastIPNumber { get; set; }
        public DateTime ChargeDate { get; set; }
        public int ServiceID { get; set; }
        public string Quantity { get; set; }
        public string Charge { get; set; }
        public bool IsGuest { get; set; }
        public bool IsNoCharge { get; set; }
        public decimal Amount { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public decimal Total { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
