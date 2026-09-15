using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodStreamDetails : Entity<string>, ITenantOwnedEntity
{

    public BloodStreamDetails(string id) : base(id) { }
    public BloodStreamDetails() : base(Guid.NewGuid().ToString()) { }

    public string? BloodStreamId { get; set; }
    public string? Dailyreview { get; set; }
    public string? Performhandhygiene { get; set; }
    public string? Maintainaseptictechnique { get; set; }
    public string? Thedisinfectionofcatheterhub { get; set; }
    public string? UseaCentralVenousCatheter { get; set; }
    public string? Usesteriletransparent { get; set; }
    public string? Gauzedressings { get; set; }
    public string? Transparentdressings { get; set; }
    public string? IVadministrationsystem { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public Guid TenantId { get; set; }

}
