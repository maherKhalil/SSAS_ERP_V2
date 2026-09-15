using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class AdmissionRequest : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string OPnumber { get; set; }
        public int DoctorID { get; set; }
        public int AdmissiontypeID { get; set; }
        public int AdmissionWardID { get; set; }
        public int AdmissionPurposeID { get; set; }
        public DateTime AdmissionDate { get; set; }
        public DateTime AdmissionTime { get; set; }
        public string Days { get; set; }
        public int SurgeryTypeID { get; set; }
        public int DeliveryTypeID { get; set; }
        public string Remarks { get; set; }
        public string Status { get; set; }
        public string IPNumber { get; set; }
        public int RoomTypeID { get; set; }
        public string BedNO { get; set; }
        public string InsuranceAuthorizationCode { get; set; }
        public string Instructions { get; set; }
        public string MedicalCondition { get; set; }
        public int MLCTypeID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int AccomodationTypeID { get; set; }
        public DateTime ExpectedDischargeDate { get; set; }
        public int EmergencyUnitID { get; set; }
        public Guid TenantId { get; set; }
    }
}
