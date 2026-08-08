namespace SSAS.Platform.Application.Localization;

public sealed record ListTenantLocalizationResourcesQuery(
  string Culture,
  int PageNumber = 1,
  int PageSize = 50,
  string? Search = null,
  string? Module = null,
  string? Group = null,
  string? Category = null,
  string? Lifecycle = "Active",
  bool OverriddenOnly = false,
  bool IncompatibleOnly = false,
  string? SecurityClassification = null);

public sealed record GetTenantLocalizationResourceQuery(string ResourceKey, string Culture);
