namespace SSAS.Platform.Application.Companies;

public sealed record CreateCompanyCommand(string CompanyCode, string CompanyName, string BaseCurrencyCode);
