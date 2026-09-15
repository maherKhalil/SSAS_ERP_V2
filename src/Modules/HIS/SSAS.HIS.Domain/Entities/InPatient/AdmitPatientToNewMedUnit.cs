using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class AdmitPatientToNewMedUnit : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string PatAccom { get; set; }
        public string PatWard { get; set; }
        public string PatRoom { get; set; }
        public string PatBed { get; set; }
        public bool IsCompanion { get; set; }
        public string CompAccom { get; set; }
        public string CompWard { get; set; }
        public string CompRoom { get; set; }
        public string CompBed { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CompanyID { get; set; }
        public string PatDiet { get; set; }
        public string CompSex { get; set; }
        public string CompDiet { get; set; }
        public string Discharge { get; set; }
        public string Reason { get; set; }
        public string DischargeType { get; set; }
        public string DischargeDoctor { get; set; }
        public string DischargeDiagnosis { get; set; }
        public DateTime DeathDate { get; set; }
        public DateTime DeathTime { get; set; }
        public string CancelAdmit { get; set; }
        public DateTime DischargeDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
