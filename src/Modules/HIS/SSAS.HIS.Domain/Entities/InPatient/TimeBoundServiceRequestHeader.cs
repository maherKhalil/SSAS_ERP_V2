using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class TimeBoundServiceRequestHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime Todate { get; set; }
        public int DoctorID { get; set; }
        public bool IsPerDay { get; set; }
        public bool IsCumulative { get; set; }
        public string AccupancyNO { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
