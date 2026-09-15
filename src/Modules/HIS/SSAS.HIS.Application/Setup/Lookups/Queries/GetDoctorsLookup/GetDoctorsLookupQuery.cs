using System;

namespace SSAS.HIS.Application.Setup.Lookups.Queries.GetDoctorsLookup;

public sealed record GetDoctorsLookupQuery();
public sealed record DoctorLookupDto(Guid Id, string Name, Guid? SpecialtyId);
