using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PatientDischarge : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientId { get; set; }
        public int DisChDoctorID { get; set; }
        public int DisChargeTypeId { get; set; }
        public int DisChDiagnosisId { get; set; }
        public string Comment { get; set; }
        public DateTime DeathDate { get; set; }
        public DateTime DeathTime { get; set; }
        public int EmergencyUnitID { get; set; }
        public DateTime CreationDate { get; set; }
        public int DischargeReasonID { get; set; }
        public string Active { get; set; }
        public DateTime DischargDate { get; set; }
        public int DischargeOrderID { get; set; }
        public string HomeMed { get; set; }
        public string BedClear { get; set; }
        public string Possisions { get; set; }
        public string IPNo { get; set; }
        public DateTime SecurityDischargeTime { get; set; }
        public int CompanyID { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
