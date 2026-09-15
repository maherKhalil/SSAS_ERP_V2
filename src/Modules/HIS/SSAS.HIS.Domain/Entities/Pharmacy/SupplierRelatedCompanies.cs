using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class SupplierRelatedCompanies : Entity<string>, ITenantOwnedEntity
{

    public SupplierRelatedCompanies(string id) : base(id) { }
    public SupplierRelatedCompanies() : base(Guid.NewGuid().ToString()) { }

    public string? SupplierRelatedCompaniesName { get; set; }
    public string? SupplierID { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
