using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ClinicSetup : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public DateTime TimeSlot { get; set; }
        public string OverBooking { get; set; }
        public int DepartmentID { get; set; }
        public int SpeciaityGroupID { get; set; }
        public string Status { get; set; }
        public int MedicalRecordLocationID { get; set; }
        public int ClinicLocationID { get; set; }
        public int PharmcyID { get; set; }
        public int ClinicTypeID { get; set; }
        public int StotreID { get; set; }
        public int PaidDealingID { get; set; }
        public int SessionID { get; set; }
        public string WalkInPatientOnly { get; set; }
        public string FutureBooking { get; set; }
        public string EndOfDay { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Creationdate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string NameRu { get; set; }
        public int CompanyID { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
