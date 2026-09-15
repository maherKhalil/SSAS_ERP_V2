using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class WardPharmacy : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SubStoreID { get; set; }
        public int PatientID { get; set; }
        public string ReceiptNo { get; set; }
        public DateTime ReceiptDate { get; set; }
        public int PaymentTypeID { get; set; }
        public int DoctorID { get; set; }
        public int PackageID { get; set; }
        public int SponsorID { get; set; }
        public string IPNO { get; set; }
        public string PrescriptionNo { get; set; }
        public DateTime PrescriptionDate { get; set; }
        public int SponsorCatagoryID { get; set; }
        public decimal SelfPayAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CollectedAmount { get; set; }
        public string Balance { get; set; }
        public string Return { get; set; }
        public string OpenInvoiceNo { get; set; }
        public decimal InvoiceAmount { get; set; }
        public string CoPay { get; set; }
        public string CoPay2 { get; set; }
        public bool IsCoPayByPer { get; set; }
        public string DischMedication { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string EnteredBy { get; set; }
        public Guid TenantId { get; set; }
    }
}
