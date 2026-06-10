using BuildCv.Domain.Common;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public sealed class GetNumberingRangesHandler(INumberingRangeStore store)
{
    public async Task<Result<IReadOnlyList<NumberingRange>>> HandleAsync(GetNumberingRangesQuery query, CancellationToken ct)
    {
        var ranges = await store.GetAllAsync(ct);
        return Result.Success(ranges);
    }
}

public sealed record GetNumberingRangesQuery;
