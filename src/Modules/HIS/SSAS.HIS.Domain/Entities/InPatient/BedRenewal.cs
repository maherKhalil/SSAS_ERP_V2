using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class BedRenewal : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Ward { get; set; }
        public string Room { get; set; }
        public string Bed { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Infexted_Renewed { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
