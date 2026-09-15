using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class MedicationDispensingPeriod_GenericNames : Entity<string>, ITenantOwnedEntity
{

    public MedicationDispensingPeriod_GenericNames(string id) : base(id) { }
    public MedicationDispensingPeriod_GenericNames() : base(Guid.NewGuid().ToString()) { }

    public string? MedicationPeriod_Id { get; set; }
    public string? GenericName_Id { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
