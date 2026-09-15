using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class GenericNamesReplacement : Entity<string>, ITenantOwnedEntity
{

    public GenericNamesReplacement(string id) : base(id) { }
    public GenericNamesReplacement() : base(Guid.NewGuid().ToString()) { }

    public string? MainGeneric { get; set; }
    public string? ReplacedGeneric { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
