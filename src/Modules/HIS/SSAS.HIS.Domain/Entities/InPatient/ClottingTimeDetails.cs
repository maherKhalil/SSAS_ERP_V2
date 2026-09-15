using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class ClottingTimeDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int MasterId { get; set; }
        public DateTime TestDate { get; set; }
        public DateTime TestTime { get; set; }
        public DateTime ClottingTime { get; set; }
        public string Dose { get; set; }
        public string AdjustedDose { get; set; }
        public int UserId { get; set; }
        public Guid TenantId { get; set; }
    }
}
