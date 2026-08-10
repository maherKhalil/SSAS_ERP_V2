using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Companies;

public sealed record DeactivateCompanyCommand(Guid CompanyId, CompanyStatusChangeReason ReasonCode, byte[] ExpectedRowVersion);
