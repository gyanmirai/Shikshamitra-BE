using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Finance.Reports.Builders;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportGenerator(
    IEnumerable<IFinanceReportBuilder> builders,
    IRepository<AcademicYear, Guid> academicYearRepository,
    FinancePhaseOneDueReports dueReports,
    FinancePhaseOnePaymentReports paymentReports)
    : ITransientDependency
{
    public async Task<FinanceReportOptionsDto> GetOptionsAsync(IReadOnlyCollection<Guid>? allowedAcademicYearIds = null)
    {
        var academicYears = await academicYearRepository.GetListAsync();
        if (allowedAcademicYearIds != null)
        {
            academicYears = academicYears
                .Where(year => allowedAcademicYearIds.Contains(year.Id))
                .ToList();
        }

        return new FinanceReportOptionsDto
        {
            ReportTypes = FinanceReportTypes.PhaseOne.Concat(builders.Select(b => b.ReportType)).Select(type => new FinanceReportOptionDto
            {
                Id = type,
                Label = type
            }).ToList(),
            AcademicYears = academicYears.Select(y => new FinanceReportOptionDto
            {
                Id = y.Id.ToString(),
                Label = y.Name
            }).ToList()
        };
    }

    public async Task<FinanceReportPreviewDto> GenerateAsync(
        FinanceReportRequestDto request,
        FinanceReportExecutionScope executionScope,
        bool exportAllRows)
    {
        var payload = new FinanceReportPreviewDto
        {
            ReportType = request.ReportType,
            NormalizedFilters = request.Filters
        };

        if (request.ReportType is FinanceReportTypes.FeeDemandSummary or FinanceReportTypes.OutstandingFees
            or FinanceReportTypes.StudentStatement)
            return await dueReports.BuildAsync(request, payload, executionScope, exportAllRows);

        if (request.ReportType is FinanceReportTypes.DailyCollections or FinanceReportTypes.ReceiptRegister
            or FinanceReportTypes.PaymentVerification)
            return await paymentReports.BuildAsync(request, payload, executionScope, exportAllRows);

        var builder = builders.FirstOrDefault(b => b.ReportType == request.ReportType)
            ?? throw new BusinessException(FinanceReportRequestGuard.UnsupportedReportType)
                .WithData("ReportType", request.ReportType);
        var result = await builder.BuildAsync(request, payload, executionScope, exportAllRows);
        foreach (var row in result.Rows)
            if (row.TryGetValue("ChequeRefNo", out var reference))
                row["ChequeRefNo"] = FinancePhaseOnePaymentReports.MaskReference(reference);
        return result;
    }
}
