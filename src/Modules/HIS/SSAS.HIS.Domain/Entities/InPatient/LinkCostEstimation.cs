using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class LinkCostEstimation : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public bool IsLinked { get; set; }
        public string EstimationNO { get; set; }
        public decimal EstimationAmount { get; set; }
        public string EstimationDays { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
