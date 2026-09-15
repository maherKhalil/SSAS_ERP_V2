using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PharmInstallationDoctorDegree : Entity<string>, ITenantOwnedEntity
{

    public PharmInstallationDoctorDegree(string id) : base(id) { }
    public PharmInstallationDoctorDegree() : base(Guid.NewGuid().ToString()) { }

    public string? DoctorDegreeId { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
