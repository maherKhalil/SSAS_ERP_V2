using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class EyesightMeasurement : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int MedicalObservationID { get; set; }
        public string RSPHDiest { get; set; }
        public string RSPHNear { get; set; }
        public string RCYLDiest { get; set; }
        public string RCYLNear { get; set; }
        public string RAXDiest { get; set; }
        public string RAXNear { get; set; }
        public string LSPHDiest { get; set; }
        public string LSPHNear { get; set; }
        public string LCYLDiest { get; set; }
        public string LCYLNear { get; set; }
        public string LAXDiest { get; set; }
        public string LAXNear { get; set; }
        public string RReading { get; set; }
        public string LReading { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string prisimbase { get; set; }
        public decimal Prisimamount { get; set; }
        public Guid TenantId { get; set; }
    }
}
