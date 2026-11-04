using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public partial class AcademicReportPerformance_Tests
{
	[Theory]
	[InlineData("class")]
	[InlineData("division")]
	[InlineData("all")]
	public async Task Dynamic_reports_keep_resolved_class_context_for_pdf_branding(string filter)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenantId = System.Guid.NewGuid();
			using var tenant = GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>().Change(tenantId);
			var assignment = await SeedAssignmentAsync(tenantId);
			var scope = await DynamicScopeAsync(assignment);
			var classId = assignment.StandardId;
			var divisionId = System.Guid.NewGuid();
			var snapshot = new AcademicDynamicReportCatalogSnapshot([new(classId, "Class 5", "class_1")],
				[new("division_1", divisionId, "A", classId, "class_1")], [new(assignment.SubjectId, "Subject", "subject_1")], []);
			var plan = new AcademicDynamicReportSpecDto { Dataset = AcademicDynamicDatasets.CurriculumSubjects, Columns = ["class"] };
			if (filter == "class") plan.Filters.ClassTokens = ["class_1"];
			if (filter == "division")
			{
				plan.Module = AcademicReportModules.Timetable;
				plan.Dataset = AcademicDynamicDatasets.TimetableSlots;
				plan.Filters.DivisionTokens = ["division_1"];
			}
			var report = await GetRequiredService<AcademicDynamicReportQueryService>().GenerateAsync(plan, scope, snapshot);
			report.NormalizedFilters.AcademicYearId.ShouldBe(scope.AcademicYearId);
			report.NormalizedFilters.ClassIds.ShouldNotBeNull().Single().ShouldBe(classId);
		});
	}
}
