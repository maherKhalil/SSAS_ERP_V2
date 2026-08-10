using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Companies;

public sealed record ArchiveCompanyCommand(Guid CompanyId, CompanyStatusChangeReason ReasonCode, byte[] ExpectedRowVersion);
