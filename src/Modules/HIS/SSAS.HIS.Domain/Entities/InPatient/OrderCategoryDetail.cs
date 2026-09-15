using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OrderCategoryDetail : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int OrderCategory_Id { get; set; }
        public int Service_Id { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
