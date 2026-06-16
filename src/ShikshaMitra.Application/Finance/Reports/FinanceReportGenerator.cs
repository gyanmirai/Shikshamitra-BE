using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Finance.Reports.Builders;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportGenerator(
    IEnumerable<IFinanceReportBuilder> builders,
    IRepository<AcademicYear, Guid> academicYearRepository)
    : ITransientDependency
{
    public async Task<FinanceReportOptionsDto> GetOptionsAsync()
    {
        var academicYears = await academicYearRepository.GetListAsync();

        return new FinanceReportOptionsDto
        {
            ReportTypes = builders.Select(b => new FinanceReportOptionDto
            {
                Id = b.ReportType,
                Label = string.Concat(b.ReportType.Select((x, i) => i > 0 && char.IsUpper(x) ? " " + x : x.ToString())) // Basic CamelCase to Space conversion for label, though could map explicitly
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
        bool exportAllRows)
    {
        var builder = builders.FirstOrDefault(b => b.ReportType == request.ReportType) 
            ?? throw new Exception($"Unsupported report type: {request.ReportType}");

        var payload = new FinanceReportPreviewDto
        {
            ReportType = request.ReportType,
            NormalizedFilters = request.Filters
        };

        return await builder.BuildAsync(request, payload, exportAllRows);
    }
}
