using SSAS.BuildingBlocks.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SSAS.HIS.Application.Setup.Lookups.Queries.GetSpecialtiesLookup;

public sealed class GetSpecialtiesLookupQueryHandler(IHisReadService readService)
{
    public async Task<Result<IEnumerable<SpecialtyLookupDto>>> HandleAsync(GetSpecialtiesLookupQuery query, CancellationToken cancellationToken = default)
    {
        var result = await readService.GetSpecialtiesLookupAsync(cancellationToken);
        return Result.Success(result);
    }
}
