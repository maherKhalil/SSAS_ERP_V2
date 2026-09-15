using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class Campaign : Entity<string>, ITenantOwnedEntity
{

    public Campaign(string id) : base(id) { }
    public Campaign() : base(Guid.NewGuid().ToString()) { }

    public string? NameEn { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? NameAr { get; set; }
    public string? CampDate { get; set; }
    public string? location { get; set; }
    public string? NoDoctors { get; set; }
    public string? NoNurses { get; set; }
    public string? NoTechnicians { get; set; }
    public string? Comments { get; set; }
    public string? NoBags { get; set; }
    public string? NoDoners { get; set; }
    public string? Code { get; set; }
    public Guid TenantId { get; set; }

}
