using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class OpeningPharmacyHeader : Entity<string>, ITenantOwnedEntity
{

    public OpeningPharmacyHeader(string id) : base(id) { }
    public OpeningPharmacyHeader() : base(Guid.NewGuid().ToString()) { }

    public string? PharmacyID { get; set; }
    public string? Status { get; set; }
    public string? Year { get; set; }
    public string? Remarks { get; set; }
    public string? Date { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? CloseYear { get; set; }
    public Guid TenantId { get; set; }

}
