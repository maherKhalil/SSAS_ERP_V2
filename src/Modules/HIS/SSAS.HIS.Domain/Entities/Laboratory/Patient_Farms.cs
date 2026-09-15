using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class Patient_Farms : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int PatientID { get; set; }
        public DateTime TestDate { get; set; }
        public int FarmID { get; set; }
        public string PatientType { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
