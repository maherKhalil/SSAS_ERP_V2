using SSAS.BuildingBlocks.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SSAS.HIS.Application.Setup.Lookups.Queries.GetClinicsLookup;

public sealed class GetClinicsLookupQueryHandler(IHisReadService readService)
{
    public async Task<Result<IEnumerable<ClinicLookupDto>>> HandleAsync(GetClinicsLookupQuery query, CancellationToken cancellationToken = default)
    {
        var result = await readService.GetClinicsLookupAsync(cancellationToken);
        return Result.Success(result);
    }
}
