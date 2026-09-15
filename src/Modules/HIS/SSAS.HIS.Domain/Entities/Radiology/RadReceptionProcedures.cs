using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class RadReceptionProcedures : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int ServicegroupID { get; set; }
        public string ProcedureSlot { get; set; }
        public int receptionID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Creationdate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
