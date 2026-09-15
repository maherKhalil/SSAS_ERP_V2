using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class SupplierContacts : Entity<string>, ITenantOwnedEntity
{

    public SupplierContacts(string id) : base(id) { }
    public SupplierContacts() : base(Guid.NewGuid().ToString()) { }

    public string? SupplierID { get; set; }
    public string? ContactName { get; set; }
    public string? ContactLocation { get; set; }
    public string? SupplierType { get; set; }
    public string? ContactType { get; set; }
    public string? ContactDescription { get; set; }
    public string? Address { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public Guid TenantId { get; set; }

}
