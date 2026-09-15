using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class GeneralNursingCarePlanHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string Code { get; set; }
        public int NurseId { get; set; }
        public int CareModeId { get; set; }
        public DateTime Date { get; set; }
        public DateTime Time { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
