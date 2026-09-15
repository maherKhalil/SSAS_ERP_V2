using SSAS.BuildingBlocks.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SSAS.HIS.Application.Setup.Lookups.Queries.GetDoctorsLookup;

public sealed class GetDoctorsLookupQueryHandler(IHisReadService readService)
{
    public async Task<Result<IEnumerable<DoctorLookupDto>>> HandleAsync(GetDoctorsLookupQuery query, CancellationToken cancellationToken = default)
    {
        var result = await readService.GetDoctorsLookupAsync(cancellationToken);
        return Result.Success(result);
    }
}
