using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class HeadUpTilt : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public string History { get; set; }
        public string ECG { get; set; }
        public string Echocardiography { get; set; }
        public string StressECG { get; set; }
        public string ProcedureStage1 { get; set; }
        public string ProcedureStage2 { get; set; }
        public string Results { get; set; }
        public string DuringStage1 { get; set; }
        public string DuringStage2 { get; set; }
        public string Recomendation { get; set; }
        public DateTime CreationDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string ModifiedBy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
