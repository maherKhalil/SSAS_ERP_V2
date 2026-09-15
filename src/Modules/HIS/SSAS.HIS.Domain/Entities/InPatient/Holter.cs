using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Holter : ITenantOwnedEntity
    {
        public int HolterId { get; set; }
        public string RecorderSerial { get; set; }
        public string Duration { get; set; }
        public int PatientId { get; set; }
        public string HolterOrder { get; set; }
        public string OverreadingPhysician { get; set; }
        public string ReferringPhysician { get; set; }
        public string OrderingPhysician { get; set; }
        public string HookUpTechnician { get; set; }
        public string AnalyzingTechnician { get; set; }
        public string IndicationDiagnosis { get; set; }
        public string Medications { get; set; }
        public string QRSComplexes { get; set; }
        public string VentricularBeats { get; set; }
        public string SupraventricularBeats { get; set; }
        public string JunctionalBeats { get; set; }
        public DateTime TotalTimeClassified { get; set; }
        public string MinHeartRate { get; set; }
        public DateTime MinHeartRateDate { get; set; }
        public string Average { get; set; }
        public string MaxHeartRate { get; set; }
        public DateTime MaxHeartRateDate { get; set; }
        public string BeatsInTachycardia { get; set; }
        public string SecondsMaxRR { get; set; }
        public string BeatsInBradycardia { get; set; }
        public DateTime SecondsMaxRRDate { get; set; }
        public bool Isolated { get; set; }
        public string Couplets { get; set; }
        public string BigeminalCycles { get; set; }
        public decimal RunsTotaling { get; set; }
        public string Beats { get; set; }
        public string SuprIsolated { get; set; }
        public string SuprCouplets { get; set; }
        public string SuprBigeminalCycles { get; set; }
        public decimal SuprRunTotaling { get; set; }
        public string SuprBeats { get; set; }
        public string BeatsLongest { get; set; }
        public string BLRunBpm { get; set; }
        public DateTime BLRunBpmDate { get; set; }
        public string BeatsFastest { get; set; }
        public string BFRunBpm { get; set; }
        public DateTime BFRunBpmDate { get; set; }
        public string Interpretation { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
