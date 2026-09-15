using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PreAnesthesiaEvaluation : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public string HistoryFrom { get; set; }
        public string HistoryFromOther { get; set; }
        public string PreviousAnesthesiaNone { get; set; }
        public string PreviousAnesthesia { get; set; }
        public string CurrentMedicationsNone { get; set; }
        public string AllergiesReaCcionNone { get; set; }
        public string AirwayType { get; set; }
        public string TMDistance { get; set; }
        public string MODistance { get; set; }
        public string NeckRom { get; set; }
        public string RespiratoryWNL { get; set; }
        public string RespiratoryEnum { get; set; }
        public string TobacooUse { get; set; }
        public string TobacooUseRR { get; set; }
        public string TobacooUsePacks { get; set; }
        public string TobacooUseForYears { get; set; }
        public string TobacooUseOut { get; set; }
        public string TobacooUseOutText { get; set; }
        public string TobacooUsePPPPE { get; set; }
        public string CardioVascularWML { get; set; }
        public string CardioVascularEnum { get; set; }
        public string VitalsHR { get; set; }
        public string VitalsBP { get; set; }
        public string VitalsJVPCVP { get; set; }
        public string VitalsPeripheralpulses { get; set; }
        public string VitalsPPPPE { get; set; }
        public string HepatoGastrointestinalWML { get; set; }
        public string HepatoGastrointestinalEnum { get; set; }
        public string EthanolUse { get; set; }
        public string EthanolUseFrequancy { get; set; }
        public string EthanolUseHxETOHAbuse { get; set; }
        public string EthanolUseOut { get; set; }
        public string EthanolUseOutText { get; set; }
        public string NeuroMusculoskeletalWML { get; set; }
        public string NeuroMusculoskeletalEnum { get; set; }
        public string RenalEndcorineWML { get; set; }
        public string RenalEndcorineEnum { get; set; }
        public string OtherWML { get; set; }
        public string OtherEnum { get; set; }
        public string FamilialAnesProblems { get; set; }
        public string FamilialAnesProblemsNotes { get; set; }
        public string SurgicalDiagnosisOrProblemList { get; set; }
        public string AsaPhysicalStatus { get; set; }
        public string NpoPerASAGuidelines { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
