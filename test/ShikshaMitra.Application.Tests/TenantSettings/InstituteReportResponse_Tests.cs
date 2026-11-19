using System;
using System.Linq;
using ShikshaMitra.TenantSettings.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteReportResponse_Tests
{
	[Theory]
	[InlineData("{\"choices\":[{\"message\":{\"content\":\"```json\\n{}\\n```\"}}]}", true)]
	[InlineData("{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"{}\"}]}]}", false)]
	public void Reads_provider_text_without_any_curriculum_dependency(string response, bool chat) => InstituteReportAiResponse.Read(response, chat).ShouldBe("{}");
	[Theory]
	[InlineData("{}", true)]
	[InlineData("{\"choices\":[]}", true)]
	[InlineData("{\"choices\":{}}", true)]
	[InlineData("{\"output\":[]}", false)]
	[InlineData("not json", false)]
	public void Missing_provider_content_has_an_institute_error(string response, bool chat) =>
		Should.Throw<BusinessException>(() => InstituteReportAiResponse.Read(response, chat)).Code.ShouldBe("InstituteReports:AiInvalidResponse");
	[Fact]
	public void Division_summary_groups_by_class_id_and_counts_zero_without_merging_duplicate_names()
	{
		var first = Guid.NewGuid(); var second = Guid.NewGuid(); var empty = Guid.NewGuid(); var a = Guid.NewGuid();
		var rows = InstituteClassDivisionSummary.Create([
			new() { StandardId = first, Standard = "Class 1", DivisionId = a, Division = "A" },
			new() { StandardId = first, Standard = "Class 1", DivisionId = a, Division = "A" },
			new() { StandardId = second, Standard = "Class 1", DivisionId = Guid.NewGuid(), Division = "A" },
			new() { StandardId = second, Standard = "Class 1", DivisionId = Guid.NewGuid(), Division = "B" },
			new() { StandardId = empty, Standard = "Class 3" }, new()
		]);
		rows.Select(x => x["divisionCount"]).ShouldBe(["1", "2", "0"]);
		var request = new InstituteReportRequest { ReportType = "class-division-summary", Filters = new() { AcademicYearId = Guid.NewGuid() }, SortField = "divisionCount", Conditions = [new() { Field = "divisionCount", Operator = "gt", Value = "0" }] };
		InstituteReportCatalog.Validate(request);
		InstituteReportProjection.Apply(rows, request).Count().ShouldBe(2);
		request.Filters.DivisionId = a;
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(request));
	}
}
