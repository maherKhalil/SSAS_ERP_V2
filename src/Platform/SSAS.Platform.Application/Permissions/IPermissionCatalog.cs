using SSAS.Platform.Domain.Permissions;

namespace SSAS.Platform.Application.Permissions;

public interface IPermissionCatalog
{
  IReadOnlyCollection<PermissionDefinition> All { get; }

  bool TryGet(string name, out PermissionDefinition permission);
}
