using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Bed : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string BedNumber { get; set; }
        public string Description { get; set; }
        public int BedStatusId { get; set; }
        public string ChildBed { get; set; }
        public int RoomId { get; set; }
        public int BedTypeId { get; set; }
        public string BedSequence { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string Code { get; set; }
        public string Location { get; set; }
        public string CountableBed { get; set; }
        public string Active { get; set; }
        public string DescriptionAr { get; set; }
        public int AssetID { get; set; }
        public bool IsBed { get; set; }
        public string BedTypeIds { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
