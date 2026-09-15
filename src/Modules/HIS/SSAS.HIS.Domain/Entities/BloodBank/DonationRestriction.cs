using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DonationRestriction : Entity<string>, ITenantOwnedEntity
{

    public DonationRestriction(string id) : base(id) { }
    public DonationRestriction() : base(Guid.NewGuid().ToString()) { }

    public string? QuestionnaireNumber { get; set; }
    public string? DonationDate { get; set; }
    public string? NumberTo { get; set; }
    public string? DonationReason { get; set; }
    public string? BladderTypes { get; set; }
    public string? ExpirationDate { get; set; }
    public string? DonationPeriod { get; set; }
    public string? BloodProductID { get; set; }
    public string? PateietCount { get; set; }
    public string? RestrictionCode { get; set; }
    public string? DBloodBank { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
