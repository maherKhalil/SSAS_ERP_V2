using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class FastEmergancy : Entity<string>, ITenantOwnedEntity
{

    public FastEmergancy(string id) : base(id) { }
    public FastEmergancy() : base(Guid.NewGuid().ToString()) { }

    public string? ComeFrom { get; set; }
    public string? patientID { get; set; }
    public string? DoctorID { get; set; }
    public string? PaymentMethodID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? Status { get; set; }
    public string? ERCode { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
