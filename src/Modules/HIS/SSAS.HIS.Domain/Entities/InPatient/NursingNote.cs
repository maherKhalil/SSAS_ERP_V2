using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class NursingNote : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string EnteredBy { get; set; }
        public string NursingNotes { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime EntryTime { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int PatientId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
