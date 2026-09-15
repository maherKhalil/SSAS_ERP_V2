using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.API;

public static class HisApiErrorMapper
{
    public static readonly ApiError NotFound = new(404, "his.not_found");
    public static readonly ApiError Conflict = new(409, "his.conflict");

    public static ApiError Map(Error error) =>
        MapCore(error).Explaining(error.Message, error.Field);

    private static ApiError MapCore(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code switch
        {
            "Patient.NotFound" => NotFound,
            "Persistence.UniqueConstraint" => Conflict,
            _ => ApiErrors.WriteFailure
        };
    }
}
