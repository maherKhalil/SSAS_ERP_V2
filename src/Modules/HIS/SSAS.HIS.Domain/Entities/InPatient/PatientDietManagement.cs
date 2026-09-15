using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PatientDietManagement : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientDietId { get; set; }
        public DateTime BreakfastTime { get; set; }
        public DateTime LunchTime { get; set; }
        public DateTime DinnerTime { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
