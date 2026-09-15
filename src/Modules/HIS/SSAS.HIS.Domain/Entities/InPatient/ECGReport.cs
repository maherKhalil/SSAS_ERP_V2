using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class ECGReport : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public DateTime ECGDate { get; set; }
        public DateTime ECGTime { get; set; }
        public int PatientId { get; set; }
        public string PatientIPNumber { get; set; }
        public int DoctorId { get; set; }
        public string Rhythm { get; set; }
        public string HeartRate { get; set; }
        public string Axis { get; set; }
        public string pwaveAmplitude { get; set; }
        public string pwaveDuration { get; set; }
        public string PRInterval { get; set; }
        public string QRSComplexMorphology { get; set; }
        public string QRSComplexDuration { get; set; }
        public string STSegment { get; set; }
        public string TWave { get; set; }
        public string Interpretion { get; set; }
        public int userid { get; set; }
        public Guid TenantId { get; set; }
    }
}
