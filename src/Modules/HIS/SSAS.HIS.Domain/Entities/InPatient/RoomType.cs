using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RoomType : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string NameEn { get; set; }
        public decimal DayPrice { get; set; }
        public int CompanyID { get; set; }
        public string Ward { get; set; }
        public string AccomodationType { get; set; }
        public string VivRoom { get; set; }
        public string Active { get; set; }
        public string IcuRoom { get; set; }
        public Guid TenantId { get; set; }
    }
}
