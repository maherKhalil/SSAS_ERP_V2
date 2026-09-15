using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DonorVitalSigns : Entity<string>, ITenantOwnedEntity
{

    public DonorVitalSigns(string id) : base(id) { }
    public DonorVitalSigns() : base(Guid.NewGuid().ToString()) { }

    public string? DonorID { get; set; }
    public string? Temp { get; set; }
    public string? Pulse { get; set; }
    public string? diaslotic { get; set; }
    public string? systolic { get; set; }
    public string? Weight { get; set; }
    public string? Height { get; set; }
    public string? Unfit { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
