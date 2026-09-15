using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class BedTracker : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string OPIP { get; set; }
        public int PatientId { get; set; }
        public int BedId { get; set; }
        public DateTime FromDateTime { get; set; }
        public DateTime ToDateTime { get; set; }
        public string AcType { get; set; }
        public Guid TenantId { get; set; }
    }
}
