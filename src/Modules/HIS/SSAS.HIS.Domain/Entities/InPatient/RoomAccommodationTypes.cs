using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RoomAccommodationTypes : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int AccommodationTypeId { get; set; }
        public Guid TenantId { get; set; }
    }
}
