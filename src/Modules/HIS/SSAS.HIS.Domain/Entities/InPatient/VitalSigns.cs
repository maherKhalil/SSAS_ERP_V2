using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class VitalSigns : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Pulse_BPH { get; set; }
        public string Urine { get; set; }
        public string Extremity { get; set; }
        public string Glucose { get; set; }
        public string TempC { get; set; }
        public string TempMode { get; set; }
        public string RespRate_MIN { get; set; }
        public string Positions { get; set; }
        public string Bowel { get; set; }
        public string MEWs { get; set; }
        public string PainScore { get; set; }
        public string Systole_MM_Hg { get; set; }
        public string LevelOfConsciouseness { get; set; }
        public string OxygenSaturation { get; set; }
        public string Diastole_MM_Hg { get; set; }
        public decimal O2Amount { get; set; }
        public string FallRisk { get; set; }
        public string Height { get; set; }
        public string Weight { get; set; }
        public int PatientID { get; set; }
        public int DoctorID { get; set; }
        public string Comment { get; set; }
        public DateTime Date { get; set; }
        public string IPOP { get; set; }
        public string BloodPressure_SYSTOLIC { get; set; }
        public string BloodPressure_DIASTOLIC { get; set; }
        public string CVP { get; set; }
        public string SourceName { get; set; }
        public string TempF { get; set; }
        public string BP_MM_Hg { get; set; }
        public int CompanyID { get; set; }
        public int BloodTransfusionId { get; set; }
        public int DonorId { get; set; }
        public string pain { get; set; }
        public string painLocation { get; set; }
        public string painDuration { get; set; }
        public string painCharac { get; set; }
        public string painFreq { get; set; }
        public string painRad { get; set; }
        public string painmodifie { get; set; }
        public string BMI { get; set; }
        public string category { get; set; }
        public Guid TenantId { get; set; }
    }
}
