using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Ward : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int WardTypeId { get; set; }
        public int WardCategoryId { get; set; }
        public int CostCenterId { get; set; }
        public int PharmcyId { get; set; }
        public int InventoryId { get; set; }
        public string MedicalServiceIdz { get; set; }
        public string Active { get; set; }
        public string Description { get; set; }
        public bool IsSecondaryWard { get; set; }
        public bool IsEndOfDay { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int FloorID { get; set; }
        public int CompanyID { get; set; }
        public string NameAr { get; set; }
        public int SpecialityID { get; set; }
        public bool IsICU { get; set; }
        public int NurseID { get; set; }
        public int BranchId { get; set; }
        public string IntensiveCareType { get; set; }
        public bool IsEmergency { get; set; }
        public Guid TenantId { get; set; }
    }
}
