using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PrescriptionAbbreviation : Entity<string>, ITenantOwnedEntity
{

    public PrescriptionAbbreviation(string id) : base(id) { }
    public PrescriptionAbbreviation() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? Quantity { get; set; }
    public string? AbbreviationName { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
