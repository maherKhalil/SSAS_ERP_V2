using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class HeadUpTiltTable : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string IPNumber { get; set; }
        public string ReferredBy { get; set; }
        public DateTime TiltDate { get; set; }
        public string History { get; set; }
        public string Investigations { get; set; }
        public string Medications { get; set; }
        public string ProcedureStage1 { get; set; }
        public string ProcedureStage2 { get; set; }
        public string ProcedureOther { get; set; }
        public string RestingHR { get; set; }
        public string RestingBPSyst { get; set; }
        public string RestingBPDiast { get; set; }
        public string Stage1HR { get; set; }
        public string Stage1BPSyst { get; set; }
        public string Stage1BPDiast { get; set; }
        public string Stage1ECG { get; set; }
        public string Stage1Symptoms { get; set; }
        public string Stage2HR { get; set; }
        public string Stage2BPSyst { get; set; }
        public string Stage2BPDiast { get; set; }
        public string Stage2ECG { get; set; }
        public string Stage2Symptoms { get; set; }
        public string Diagnosis { get; set; }
        public string Recommendation { get; set; }
        public DateTime CreationDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string ModifiedBy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
