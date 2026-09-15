using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class ReturnDispenseBloodBags : Entity<string>, ITenantOwnedEntity
{

    public ReturnDispenseBloodBags(string id) : base(id) { }
    public ReturnDispenseBloodBags() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? BagID { get; set; }
    public string? BagCode { get; set; }
    public string? OP_IPNumber { get; set; }
    public string? DoctorID { get; set; }
    public string? ServiceID { get; set; }
    public string? ReturnAmount { get; set; }
    public string? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? ReturnDate { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public string? EntryCodes { get; set; }
    public Guid TenantId { get; set; }

}
