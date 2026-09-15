using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ICDCode : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public bool ISInPatient { get; set; }
        public bool ISOutPatient { get; set; }
        public string OPNO { get; set; }
        public DateTime OPDate { get; set; }
        public string ICDCodeValue { get; set; }
        public string ICDDesc { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
