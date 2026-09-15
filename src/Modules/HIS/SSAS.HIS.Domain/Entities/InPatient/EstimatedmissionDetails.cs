using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class EstimatedmissionDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int EstimatedmissionID { get; set; }
        public int ServiceID { get; set; }
        public string Units { get; set; }
        public decimal DiscountAmount { get; set; }
        public string DiscountPrecentage { get; set; }
        public decimal NetAmount { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
