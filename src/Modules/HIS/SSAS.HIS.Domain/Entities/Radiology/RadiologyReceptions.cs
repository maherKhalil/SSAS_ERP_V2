using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class RadiologyReceptions : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public DateTime TimeSlot { get; set; }
        public int DepartmentID { get; set; }
        public int SpeciaityGroupID { get; set; }
        public bool IsActive { get; set; }
        public string Location { get; set; }
        public int ReceptionTypeID { get; set; }
        public int SessionID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string Emp_ReceptionAdmin { get; set; }
        public int CompanyID { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
