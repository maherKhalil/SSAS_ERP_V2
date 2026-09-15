using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class clabsibundleDetails : Entity<string>, ITenantOwnedEntity
{

    public clabsibundleDetails(string id) : base(id) { }
    public clabsibundleDetails() : base(Guid.NewGuid().ToString()) { }

    public string? clabsibundletId { get; set; }
    public string? Keepcatheterproperlysecuredtopreventmovementurethraltraction { get; set; }
    public string? bagbelow { get; set; }
    public string? bagonce { get; set; }
    public string? urineflow { get; set; }
    public string? Maintainclosed { get; set; }
    public string? Performhand { get; set; }
    public string? Obtainurinesampleasepticall { get; set; }
    public string? techniquedisconnection { get; set; }
    public string? Perinealhygiene { get; set; }
    public string? Reassess { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public Guid TenantId { get; set; }

}
