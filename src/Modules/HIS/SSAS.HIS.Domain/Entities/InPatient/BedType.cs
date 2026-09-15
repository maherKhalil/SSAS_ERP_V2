using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class BedType : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string TypeImage { get; set; }
        public int CompanyID { get; set; }
        public string NameAr { get; set; }
        public string NameKa { get; set; }
        public Guid TenantId { get; set; }
    }
}
