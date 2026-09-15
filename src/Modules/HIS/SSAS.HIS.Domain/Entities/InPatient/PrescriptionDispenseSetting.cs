using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PrescriptionDispenseSetting : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Days { get; set; }
        public string Prescription { get; set; }
        public string NoOfDays { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
