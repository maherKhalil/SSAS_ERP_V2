using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class WardPharmacyPaymentDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int WardPharmacyId { get; set; }
        public string ReceiptNo { get; set; }
        public int PaymentModeId { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string InstrumentNo { get; set; }
        public DateTime InstrumentDate { get; set; }
        public string Bank { get; set; }
        public string Remark { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
