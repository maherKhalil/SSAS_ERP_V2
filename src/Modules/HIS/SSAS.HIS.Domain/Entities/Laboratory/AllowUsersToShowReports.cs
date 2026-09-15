using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class AllowUsersToShowReports : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int EmployeeID { get; set; }
        public string ResultEntryHeaderD { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
