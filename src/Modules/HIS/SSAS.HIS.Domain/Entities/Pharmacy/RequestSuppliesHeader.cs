using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class RequestSuppliesHeader : Entity<string>, ITenantOwnedEntity
{

    public RequestSuppliesHeader(string id) : base(id) { }
    public RequestSuppliesHeader() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? PatientTypeID { get; set; }
    public string? OP_IPNo { get; set; }
    public string? RequestNo { get; set; }
    public string? DoctorID { get; set; }
    public string? RequestStatus { get; set; }
    public string? SubStoreID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Status { get; set; }
    public string? OutPatName { get; set; }
    public string? outPrescriptionDate { get; set; }
    public string? PrescriptionHeaderID { get; set; }
    public string? InsuranceId { get; set; }
    public string? branchId { get; set; }
    public string? DiscountPercent { get; set; }
    public string? DiscountAmount { get; set; }
    public Guid TenantId { get; set; }

}
