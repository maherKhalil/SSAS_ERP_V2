using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class DeliveryDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string IdentificationName { get; set; }
        public int DeliveryModeID { get; set; }
        public int SexId { get; set; }
        public DateTime DateofBirth { get; set; }
        public DateTime TimeofBirth { get; set; }
        public string Condition { get; set; }
        public string DeliveryTerm { get; set; }
        public string Sterilization { get; set; }
        public int BabyWardID { get; set; }
        public int BedID { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int DeliveryId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
