using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DBloodBags : Entity<string>, ITenantOwnedEntity
{

    public DBloodBags(string id) : base(id) { }
    public DBloodBags() : base(Guid.NewGuid().ToString()) { }

    public string? DonorID { get; set; }
    public string? DonationType { get; set; }
    public string? donationDate { get; set; }
    public string? ExpiryDate { get; set; }
    public string? Code { get; set; }
    public string? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? BloodGroup_ID { get; set; }
    public string? Size { get; set; }
    public string? BagType { get; set; }
    public string? CampaignID { get; set; }
    public string? OutSourceID { get; set; }
    public string? Status { get; set; }
    public string? FirstResult { get; set; }
    public string? SecondResult { get; set; }
    public string? FinalResult { get; set; }
    public string? BagDeal { get; set; }
    public string? VoucherID { get; set; }
    public string? Amount { get; set; }
    public string? ParentID { get; set; }
    public string? ScrapReson { get; set; }
    public string? ScrapDate { get; set; }
    public string? Serial { get; set; }
    public string? Refrigerator { get; set; }
    public string? Volume { get; set; }
    public string? TubeNo { get; set; }
    public string? BladderType { get; set; }
    public string? QuestionnaireNumber { get; set; }
    public string? DonationReason { get; set; }
    public string? DonationPeriod { get; set; }
    public string? DeliveryDate { get; set; }
    public string? Receipt { get; set; }
    public string? PurchasePrice { get; set; }
    public string? PatientId { get; set; }
    public string? NumberTo { get; set; }
    public string? EntryCodes { get; set; }
    public string? StoreID { get; set; }
    public string? AdjustmentEntryID { get; set; }
    public string? AdjustmentEntryState { get; set; }
    public Guid TenantId { get; set; }

}
