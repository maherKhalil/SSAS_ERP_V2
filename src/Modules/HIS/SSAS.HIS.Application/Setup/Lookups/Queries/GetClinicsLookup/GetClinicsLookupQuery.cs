using System;

namespace SSAS.HIS.Application.Setup.Lookups.Queries.GetClinicsLookup;

public sealed record GetClinicsLookupQuery();
public sealed record ClinicLookupDto(int Id, string Code, string NameArabic, string NameEnglish);
