using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DispenseBloodBags : Entity<string>, ITenantOwnedEntity
{

    public DispenseBloodBags(string id) : base(id) { }
    public DispenseBloodBags() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? BloodGroup { get; set; }
    public string? BagID { get; set; }
    public string? BagSource { get; set; }
    public string? DonorID { get; set; }
    public string? ChequeDonor { get; set; }
    public string? ChequeLocation { get; set; }
    public string? ChequeExpiryDate { get; set; }
    public string? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CompanyID { get; set; }
    public string? DispenseDate { get; set; }
    public string? BagCode { get; set; }
    public string? OP_IPNumber { get; set; }
    public string? DoctorID { get; set; }
    public string? ServiceID { get; set; }
    public string? ReqBloodGroup { get; set; }
    public string? ReqProduct { get; set; }
    public string? ReceiverName { get; set; }
    public string? Quantity { get; set; }
    public string? DepartmentAndWard { get; set; }
    public string? InvestDtlID { get; set; }
    public string? transform { get; set; }
    public Guid TenantId { get; set; }

}
