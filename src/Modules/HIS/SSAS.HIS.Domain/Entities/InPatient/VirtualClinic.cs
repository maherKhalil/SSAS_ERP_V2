using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class VirtualClinic : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public int MeetingId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string status { get; set; }
        public Guid TenantId { get; set; }
    }
}
