using BuildCv.Domain.Common;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public sealed class GetCompanyHandler(IInvoiceProvider provider)
{
    public async Task<Result<CompanyInfo>> HandleAsync(GetCompanyQuery query, CancellationToken ct)
    {
        var company = await provider.GetCompanyAsync(ct);
        return Result.Success(company);
    }
}

public sealed record GetCompanyQuery;
