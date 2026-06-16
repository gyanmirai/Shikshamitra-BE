using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Reports.Builders;

public interface IFinanceReportBuilder
{
    string ReportType { get; }
    Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, bool exportAllRows);
}
