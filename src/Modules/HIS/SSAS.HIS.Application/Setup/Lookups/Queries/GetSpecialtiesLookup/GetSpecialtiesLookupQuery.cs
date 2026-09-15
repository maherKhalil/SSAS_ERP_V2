using System;

namespace SSAS.HIS.Application.Setup.Lookups.Queries.GetSpecialtiesLookup;

public sealed record GetSpecialtiesLookupQuery();
public sealed record SpecialtyLookupDto(Guid Id, string Name);
