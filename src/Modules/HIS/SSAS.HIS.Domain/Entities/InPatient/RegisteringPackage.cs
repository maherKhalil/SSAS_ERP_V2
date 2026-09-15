using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RegisteringPackage : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int PackageID { get; set; }
        public decimal Amount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string DiscountPrecentage { get; set; }
        public int TypeID { get; set; }
        public string IPNumber { get; set; }
        public decimal InstalmentAmount { get; set; }
        public decimal ColectedAmount { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal BalanceAmount { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
