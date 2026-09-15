using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class Labs : ITenantOwnedEntity
    {
        public string LabCode { get; set; }
        public string LabName { get; set; }
        public int LabAdminId { get; set; }
        public string Location { get; set; }
        public int SessionId { get; set; }
        public string labType { get; set; }
        public bool IsActive { get; set; }
        public int CompanyID { get; set; }
        public string LabNameAr { get; set; }
        public string Code { get; set; }
        public Guid TenantId { get; set; }
    }
}
