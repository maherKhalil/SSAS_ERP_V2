using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Cardiac_Electrphysiology : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int PatintID { get; set; }
        public DateTime ReportDate { get; set; }
        public int DoctorID { get; set; }
        public string History { get; set; }
        public string Medications { get; set; }
        public string ECG { get; set; }
        public string CardiacProcedure { get; set; }
        public string Ventricular_Pacing { get; set; }
        public string Atrial_Pacing { get; set; }
        public string RadioFreq_Ablation { get; set; }
        public string Ablation_Target { get; set; }
        public string RFCurrent { get; set; }
        public string ECG_DuringRF { get; set; }
        public string Diagnosis { get; set; }
        public string Recommendation { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
