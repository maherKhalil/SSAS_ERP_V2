using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class DischargeSummary : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int DestinationID { get; set; }
        public int DischargeTypeID { get; set; }
        public string Remarks { get; set; }
        public DateTime DischargeDate { get; set; }
        public DateTime DischargeTime { get; set; }
        public DateTime Approvedate { get; set; }
        public DateTime ApproveTime { get; set; }
        public int ApproveDoctorID { get; set; }
        public string PresentingComplaint { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int DoctorID { get; set; }
        public string DestinationText { get; set; }
        public int CompanyID { get; set; }
        public string CoMorbidConditions { get; set; }
        public string SignificantPhysicalandOtherFindings { get; set; }
        public string IntrahospitalCourse { get; set; }
        public int INVRDId { get; set; }
        public string DiagnosticandTherapeuticProceduresPerformed { get; set; }
        public string SignificantMedicationsandOtherTreatments { get; set; }
        public int PresDId { get; set; }
        public string FollowupInstructions { get; set; }
        public DateTime NextbookedOPDVisitDate { get; set; }
        public string whentoseekEmergencyMedicalCare { get; set; }
        public string IPNumber { get; set; }
        public Guid TenantId { get; set; }
    }
}
