using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionAssessmentColumnFilters_Tests
{
	private static AdmissionAssessmentColumnFilterRow[] Rows => [
		new() { StageId = Guid.NewGuid(), ApplicationNumber = "APP-001", ClassName = "Grade 1", SessionName = "Morning", Attendance = "present", Outcome = "passed" },
		new() { StageId = Guid.NewGuid(), ApplicationNumber = "APP-002", ClassName = "Grade 2", SessionName = "Afternoon", Attendance = "absent", Outcome = "failed" },
		new() { StageId = Guid.NewGuid(), ApplicationNumber = "APP-003", ClassName = "Grade 1", SessionName = "", Attendance = "notRequired", Outcome = "waived", Status = "waived" }
	];

	[Theory]
	[InlineData("applicationNumber", "contains", "002", "string", 1)]
	[InlineData("className", "equals", "grade 1", "string", 2)]
	[InlineData("sessionName", "startsWith", "mor", "string", 1)]
	[InlineData("sessionName", "endsWith", "noon", "string", 1)]
	[InlineData("attendance", "equals", "absent", "enum", 1)]
	[InlineData("result", "equals", "passed", "enum", 1)]
	[InlineData("result", "equals", "waived", "enum", 1)]
	[InlineData("status", "equals", "waived", "enum", 1)]
	public void Filters_scalar_values_across_the_whole_population(string column, string type, string value, string valueType, int count)
	{
		var criteria = JsonSerializer.Serialize(new[] { new { column, type, value, valueType } });
		AdmissionAssessmentColumnFilters.Apply(Rows, criteria).Count().ShouldBe(count);
	}

	[Fact]
	public void Combines_before_count_and_page()
	{
		var filtered = AdmissionAssessmentColumnFilters.Apply(Rows, """[{"column":"className","type":"equals","value":"Grade 1","valueType":"string"},{"column":"attendance","type":"equals","value":"notRequired","valueType":"enum"}]""").ToList();
		filtered.Count.ShouldBe(1);
		filtered.Skip(0).Take(1).Single().ApplicationNumber.ShouldBe("APP-003");
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("[null]")]
	[InlineData("[{\"column\":\"nextAction\",\"type\":\"equals\",\"value\":\"Continue\",\"valueType\":\"string\"}]")]
	[InlineData("[{\"column\":\"attendance\",\"type\":\"equals\",\"value\":\"other\",\"valueType\":\"enum\"}]")]
	[InlineData("[{\"column\":\"className\",\"type\":\"contains\",\"value\":{},\"valueType\":\"string\"}]")]
	public void Rejects_unknown_or_malformed_filters(string filters) => Should.Throw<BusinessException>(() => AdmissionAssessmentColumnFilters.Apply(Rows, filters).ToList());
}
