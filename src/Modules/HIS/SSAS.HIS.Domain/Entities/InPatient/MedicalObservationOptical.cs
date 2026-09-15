using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservationOptical : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int MedicalObservationId { get; set; }
        public int ServiceID { get; set; }
        public string ServiceName { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int LensTypeID { get; set; }
        public string LensTypeName { get; set; }
        public int insuranceId { get; set; }
        public Guid TenantId { get; set; }
    }
}
