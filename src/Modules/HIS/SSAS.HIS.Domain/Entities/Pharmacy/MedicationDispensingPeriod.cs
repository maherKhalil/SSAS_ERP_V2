using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class MedicationDispensingPeriod : Entity<string>, ITenantOwnedEntity
{

    public MedicationDispensingPeriod(string id) : base(id) { }
    public MedicationDispensingPeriod() : base(Guid.NewGuid().ToString()) { }

    public string? NameArabic { get; set; }
    public string? NameEnglish { get; set; }
    public string? FirstTimeWarning { get; set; }
    public string? SecondTimeWarning { get; set; }
    public string? TradeName { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
