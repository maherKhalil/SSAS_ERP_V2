using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OperationTheatreDailyDuty : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int OperationTheatreId { get; set; }
        public int CostCenterId { get; set; }
        public int SessionId { get; set; }
        public DateTime DutyDate { get; set; }
        public DateTime FromTime { get; set; }
        public DateTime ToTime { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
