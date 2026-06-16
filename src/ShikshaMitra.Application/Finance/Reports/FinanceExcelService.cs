using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Finance.Reports;

public class FinanceExcelService : ITransientDependency
{
    public Task<byte[]> GenerateReportExcelAsync(FinanceReportPreviewDto data)
    {
        // TODO: Implement actual ClosedXML logic
        return Task.FromResult(Array.Empty<byte>());
    }
}
