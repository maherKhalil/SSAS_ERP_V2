using SSAS.BuildingBlocks.Tenancy.Permissions;

namespace SSAS.HIS.Application.Permissions;

public sealed class HisPermissionCatalogContributor : IPermissionCatalogContributor
{
    private static readonly ModulePermissionDefinition[] Definitions =
    [
        new(HisPermissionNames.ManageRegistration,
            "Manage patient registration.")
    ];

    public IReadOnlyCollection<ModulePermissionDefinition> Permissions => Definitions;
}
