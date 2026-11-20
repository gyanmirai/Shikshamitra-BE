using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	public async Task<LibraryReportOptionsDto> GetOptionsAsync(LibraryReportExecutionScope executionScope)
	{
		EnsureExecutionScope(executionScope);
		var allowedYearIds = executionScope.AllowedAcademicYearIds;
		var categories = await asyncExecuter.ToListAsync((await categoryRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenant.Id).OrderBy(x => x.Name));
		var sections = await asyncExecuter.ToListAsync((await sectionRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenant.Id).OrderBy(x => x.SectionName));
		var academicYears = await asyncExecuter.ToListAsync((await academicYearRepository.GetQueryableAsync())
			.Where(x => x.TenantId == tenant.Id && allowedYearIds.Contains(x.Id)).OrderByDescending(x => x.StartDate));

		return new LibraryReportOptionsDto
		{
			ReportTypes = LibraryReportCatalog.SupportedReports
				.Select(x => new LibraryReportOptionDto
				{
					Id = x.Id,
					Label = x.Id,
					Category = x.Category
				})
				.ToList(),
			ComingSoonReportTypes = LibraryReportCatalog.ComingSoonReports
				.Select(x => new LibraryReportOptionDto
				{
					Id = x.Id,
					Label = x.Id,
					Category = x.Category,
					IsComingSoon = true
				})
				.ToList(),
			Categories = categories.Select(x => new LibraryReportOptionDto { Id = x.Id.ToString(), Label = x.Name }).ToList(),
			Sections = sections.Select(x => new LibraryReportOptionDto { Id = x.Id.ToString(), Label = x.SectionName ?? string.Empty }).ToList(),
			CopyStatuses = LibraryReportCatalog.BuildCopyStatusOptions(),
			IssueStatuses = LibraryReportCatalog.BuildIssueStatusOptions(),
			ChargeStatuses = LibraryReportCatalog.BuildChargeStatusOptions(),
			Formats = LibraryReportCatalog.BuildFormatOptions(),
			AcademicYears = academicYears.Select(x => new LibraryReportOptionDto { Id = x.Id.ToString(), Label = x.Name }).ToList()
		};
	}
}
