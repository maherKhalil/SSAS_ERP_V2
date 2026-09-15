using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DonorRegistration : Entity<string>, ITenantOwnedEntity
{

    public DonorRegistration(string id) : base(id) { }
    public DonorRegistration() : base(Guid.NewGuid().ToString()) { }

    public string? FirstNameLatin { get; set; }
    public string? SecoendNameLatin { get; set; }
    public string? ThirdNameLatin { get; set; }
    public string? LastNameLatin { get; set; }
    public string? FirstNameLocal { get; set; }
    public string? SecoendNameLocal { get; set; }
    public string? ThirdNameLocal { get; set; }
    public string? LastNameLocal { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Birthdate { get; set; }
    public string? Age { get; set; }
    public string? Address { get; set; }
    public string? NationalID { get; set; }
    public string? Telephone { get; set; }
    public string? BloodGroupID { get; set; }
    public string? ISactive { get; set; }
    public string? ClincalHistory { get; set; }
    public string? RelativeTelephone { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public string? CompanyID { get; set; }
    public string? Occupation { get; set; }
    public string? LastDonationDate { get; set; }
    public Guid TenantId { get; set; }

}
