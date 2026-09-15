using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class PatientFarm_Details : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int PatientFarmID { get; set; }
        public int AntibioticID { get; set; }
        public string TestResult { get; set; }
        public DateTime AntibioticTestDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
