using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class SubstoreAuthority : Entity<string>, ITenantOwnedEntity
{

    public SubstoreAuthority(string id) : base(id) { }
    public SubstoreAuthority() : base(Guid.NewGuid().ToString()) { }

    public string? SubStoreID { get; set; }
    public string? UserID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? IsLPO { get; set; }
    public string? IsEPO { get; set; }
    public string? IsGRN { get; set; }
    public string? IsCashGRN { get; set; }
    public string? IsAddition { get; set; }
    public string? IsDispense { get; set; }
    public string? isReturn { get; set; }
    public string? IsUpdateStock { get; set; }
    public string? IsIToD { get; set; }
    public string? IsIToPharmacy { get; set; }
    public string? ReturnToPatient { get; set; }
    public string? ReturnToSupplier { get; set; }
    public Guid TenantId { get; set; }

}
