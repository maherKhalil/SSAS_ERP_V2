using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DailyCloseDate : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public DateTime CloseDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
