using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class FacilityMaster : Entity<string>, ITenantOwnedEntity
{

    public FacilityMaster(string id) : base(id) { }
    public FacilityMaster() : base(Guid.NewGuid().ToString()) { }

    public string? FacilityLatinName { get; set; }
    public string? FacilityLocalName { get; set; }
    public string? FacilityCode { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? ISActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
