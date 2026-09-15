using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class NormalEmergancy : Entity<string>, ITenantOwnedEntity
{

    public NormalEmergancy(string id) : base(id) { }
    public NormalEmergancy() : base(Guid.NewGuid().ToString()) { }

    public string? patientID { get; set; }
    public string? CameFrom { get; set; }
    public string? PaymentMethodID { get; set; }
    public string? DoctorID { get; set; }
    public string? Status { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? ERCode { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
