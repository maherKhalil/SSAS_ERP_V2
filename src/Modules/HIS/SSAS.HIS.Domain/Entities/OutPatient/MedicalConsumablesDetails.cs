using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class MedicalConsumablesDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int MedicalConsumablesID { get; set; }
        public int ItemID { get; set; }
        public int UniteID { get; set; }
        public string Quantity { get; set; }
        public decimal Amount { get; set; }
        public bool ISIncluded { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int SubstoreID { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
