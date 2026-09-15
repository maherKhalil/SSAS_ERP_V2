using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class AdmitPatients : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int DoctorID { get; set; }
        public string IPNumber { get; set; }
        public bool IsDayAdmission { get; set; }
        public string InsuranceAuthorizationCode { get; set; }
        public int AdmissionRequestID { get; set; }
        public int AdmissionWardID { get; set; }
        public string Days { get; set; }
        public int MLCTypeID { get; set; }
        public DateTime AdmissionDate { get; set; }
        public DateTime AdmissionTime { get; set; }
        public string BedNO { get; set; }
        public int RoomTypeID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int DischargeStatusId { get; set; }
        public DateTime ExpectedDateOfDischarge { get; set; }
        public DateTime ExpectedTimeOfDischarge { get; set; }
        public int CompanyID { get; set; }
        public int EmergencyUnitID { get; set; }
        public int AccomodationTypeId { get; set; }
        public int PatLimitID { get; set; }
        public string AdmitType { get; set; }
        public int AdmitNurseID { get; set; }
        public string NeedEscort { get; set; }
        public int EscortBedID { get; set; }
        public string Remarks { get; set; }
        public int AdmitDoctorID { get; set; }
        public string AdmitUrgency { get; set; }
        public bool IsIsolation { get; set; }
        public string AdmitionType { get; set; }
        public bool IsReferral { get; set; }
        public string RefDoctorName { get; set; }
        public string RefClinicName { get; set; }
        public int SponsorID { get; set; }
        public int BranchId { get; set; }
        public string ToOPD { get; set; }
        public string EncounterType { get; set; }
        public string EncounterAdmit { get; set; }
        public string Encounterstatus { get; set; }
        public string serviceType { get; set; }
        public string careteamrole { get; set; }
        public Guid TenantId { get; set; }
    }
}
