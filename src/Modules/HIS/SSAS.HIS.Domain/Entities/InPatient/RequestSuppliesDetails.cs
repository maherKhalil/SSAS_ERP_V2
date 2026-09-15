using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RequestSuppliesDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int RequestSuppliesHeaderID { get; set; }
        public int ItemID { get; set; }
        public int UnitConversionID { get; set; }
        public string ReturnedQty { get; set; }
        public string Quantity { get; set; }
        public bool IsIncluded { get; set; }
        public int StockBatchId { get; set; }
        public bool IsApproved { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int CurrencyID { get; set; }
        public string ConvValue { get; set; }
        public decimal Price { get; set; }
        public string StockControlDetailList { get; set; }
        public bool ISCash { get; set; }
        public int InsuranceId { get; set; }
        public string Dispense { get; set; }
        public string Verified { get; set; }
        public DateTime VerifiedDate { get; set; }
        public string VerifiedBY { get; set; }
        public bool IsCancelled { get; set; }
        public Guid TenantId { get; set; }
    }
}
