using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RegisteringPackageinstallment : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int PackageID { get; set; }
        public string IPNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal Amount { get; set; }
        public string Duration { get; set; }
        public string Period { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
