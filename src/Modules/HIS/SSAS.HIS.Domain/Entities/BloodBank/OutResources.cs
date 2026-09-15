using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class OutResources : Entity<string>, ITenantOwnedEntity
{

    public OutResources(string id) : base(id) { }
    public OutResources() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? OutResourcesEnglishName { get; set; }
    public string? OutResourcesArabicName { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Administrator { get; set; }
    public string? Active { get; set; }
    public string? Account { get; set; }
    public string? ReturnPeriodNumber { get; set; }
    public string? ReturnPeriodType { get; set; }
    public string? WarningPeriodNumber { get; set; }
    public string? WarningPeriodType { get; set; }
    public string? CompanyID { get; set; }
    public string? IsLab { get; set; }
    public string? Labresponsibleperson { get; set; }
    public string? LabPhone { get; set; }
    public string? LabApi { get; set; }
    public Guid TenantId { get; set; }

}
