using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class TimeBoundServiceRequestDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int TimeBoundServiceRequestID { get; set; }
        public int ServiceID { get; set; }
        public int IssueUnitID { get; set; }
        public string UnitCharge { get; set; }
        public DateTime TimeBound { get; set; }
        public DateTime FromTime { get; set; }
        public DateTime ToTime { get; set; }
        public decimal TotalUnits { get; set; }
        public decimal TotalAmount { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
