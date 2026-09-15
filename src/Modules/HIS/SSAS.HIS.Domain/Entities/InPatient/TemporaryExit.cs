using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class TemporaryExit : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PateintID { get; set; }
        public string PatientIPNo { get; set; }
        public int DoctorID { get; set; }
        public DateTime GoOutTime { get; set; }
        public DateTime BackTime { get; set; }
        public string DoctorApproveAttachmentURL { get; set; }
        public string PatientSignConsentAttachmentURL { get; set; }
        public DateTime ActualBackTime { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string LastModificationBy { get; set; }
        public DateTime LastModificationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
