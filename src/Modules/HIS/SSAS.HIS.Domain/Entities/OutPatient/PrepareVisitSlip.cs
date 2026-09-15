using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class PrepareVisitSlip : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string VisitType { get; set; }
        public int ScheduleAppointmentID { get; set; }
        public int DepartmentID { get; set; }
        public int DoctorsID { get; set; }
        public int SessionID { get; set; }
        public DateTime Time { get; set; }
        public DateTime TimeOfVisit { get; set; }
        public int referralDepartmentID { get; set; }
        public int referralClinicID { get; set; }
        public int referralDoctorsID { get; set; }
        public int MedicoLegalID { get; set; }
        public string VisitStatus { get; set; }
        public string OPnumber { get; set; }
        public string VisitNo { get; set; }
        public string Remarks { get; set; }
        public string NoCharge { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string Purpose { get; set; }
        public string OPNumberStatus { get; set; }
        public int CompanyID { get; set; }
        public bool IsApproved { get; set; }
        public string VisitSlipStatus { get; set; }
        public string CancelReason { get; set; }
        public string referralDoctorsPerc { get; set; }
        public string EndTreatmentCycle { get; set; }
        public bool IsOnlinePayment { get; set; }
        public DateTime OvarTimeOnline { get; set; }
        public int BranchId { get; set; }
        public bool IsConfirmthetermsandconditions { get; set; }
        public bool IsVirtual { get; set; }
        public string invDtl { get; set; }
        public string VisitReason { get; set; }
        public string Encounterstatus { get; set; }
        public string serviceType { get; set; }
        public string careteamRole { get; set; }
        public Guid TenantId { get; set; }
    }
}
