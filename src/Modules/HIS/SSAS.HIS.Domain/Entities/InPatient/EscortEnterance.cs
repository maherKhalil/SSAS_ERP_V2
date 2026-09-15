using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class EscortEnterance : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int EscortId { get; set; }
        public string EnteranceType { get; set; }
        public DateTime EnteranceTime { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
