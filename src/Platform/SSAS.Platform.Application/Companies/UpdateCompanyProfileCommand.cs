namespace SSAS.Platform.Application.Companies;

public sealed record UpdateCompanyProfileCommand(Guid CompanyId, string CompanyName, byte[] ExpectedRowVersion);
