using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class BedStatus : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string StatusImage { get; set; }
        public int CompanyID { get; set; }
        public string NameAr { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
