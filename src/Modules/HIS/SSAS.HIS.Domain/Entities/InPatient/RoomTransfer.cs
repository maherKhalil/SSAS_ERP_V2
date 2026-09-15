using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RoomTransfer : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string IPNumber { get; set; }
        public int FromWardId { get; set; }
        public int ToWardId { get; set; }
        public int FromBedId { get; set; }
        public int ToBedId { get; set; }
        public DateTime TransferDate { get; set; }
        public DateTime TransferTime { get; set; }
        public string Remarks { get; set; }
        public int AdmitPateintId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
