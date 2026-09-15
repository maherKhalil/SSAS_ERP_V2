using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CancelIntialDischarge : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public int InitialDischID { get; set; }
        public string Comment { get; set; }
        public Guid TenantId { get; set; }
    }
}
