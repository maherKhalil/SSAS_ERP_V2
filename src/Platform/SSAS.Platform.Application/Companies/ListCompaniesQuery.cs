using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Companies;

public sealed record ListCompaniesQuery(CompanyStatus? Status = null, int PageNumber = 1, int PageSize = 50);
