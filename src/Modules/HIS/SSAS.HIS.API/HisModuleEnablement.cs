using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.HIS.API;

public sealed class HisModuleEnablement : IModuleEnablementDescriptor
{
    public const string Key = "HIS";
    public string ModuleKey => Key;
}
