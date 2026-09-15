using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Room : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; }
        public int RoomTypeId { get; set; }
        public int CostCenterId { get; set; }
        public int WardId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int AccommodationTypeId { get; set; }
        public string Code { get; set; }
        public string AccommodationTypesIDz { get; set; }
        public string Active { get; set; }
        public string VIP { get; set; }
        public string Phone { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
