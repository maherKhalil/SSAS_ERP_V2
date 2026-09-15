using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class BedWardArrangement : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int WardId { get; set; }
        public string RowsNumber { get; set; }
        public string BedCount { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
