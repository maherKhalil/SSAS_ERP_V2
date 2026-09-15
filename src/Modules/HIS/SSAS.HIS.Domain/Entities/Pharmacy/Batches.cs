using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Batches : Entity<string>, ITenantOwnedEntity
{

    public Batches(string id) : base(id) { }
    public Batches() : base(Guid.NewGuid().ToString()) { }

    public string? BatchNo { get; set; }
    public string? GRNDetailsId { get; set; }
    public string? ExpiryDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? LPODetailsId { get; set; }
    public Guid TenantId { get; set; }

}
