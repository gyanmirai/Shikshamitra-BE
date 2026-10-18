using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrReportingEngine_Tests
{
	private static HrReportDesignDto Design(string key) => HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:" + key).Definition;
	[Fact]
	public void Every_Starter_Should_Be_A_Valid_Design_With_An_Explicit_Row_Meaning()
	{
		var templates = HrReportingCatalogue.BuiltIns(); templates.Count.ShouldBe(20);
		foreach (var template in templates)
		{
			var dataset = HrReportingEngine.Validate(template.Definition);
			dataset.RowMeaning.ShouldNotBeNullOrWhiteSpace();
			template.IsBuiltIn.ShouldBeTrue(); template.PublishedRevision.ShouldBe(1);
		}
	}
	[Fact]
	public void Relative_Period_Should_Resolve_Again_Without_Changing_The_Definition()
	{
		var d = Design("in-out");
		var august = HrReportingEngine.ResolveParameters(d, new(), new DateTimeOffset(2026, 8, 31, 20, 0, 0, TimeSpan.Zero), "Asia/Kolkata");
		var october = HrReportingEngine.ResolveParameters(d, new(), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "Asia/Kolkata");
		august.FromDate.ShouldBe(new DateOnly(2026, 9, 1)); october.FromDate.ShouldBe(new DateOnly(2026, 10, 1));
		d.DefaultPeriod.ShouldBe("thisMonth");
	}
	[Fact]
	public void Filters_And_Totals_Should_Include_Rows_Beyond_The_Old_20000_Row_Cap()
	{
		var d = Design("salary-register");
		d.Filters.Conditions = [new() { Field = "netPay", Operator = "gte", Value = "20000" }];
		var rows = Enumerable.Range(0, 40001).Select(i => new Dictionary<string, object?> { ["employeeCode"] = i.ToString(), ["netPay"] = (decimal) i }).ToList();
		var result = HrReportingEngine.Execute(d, new(), rows);
		result.Rows.Count.ShouldBe(20001); result.Totals["netPay"].ShouldBe(600030000m);
	}
	[Theory]
	[InlineData("salary-register")]
	[InlineData("monthly-attendance")]
	public void Monthly_Registers_Should_Reject_Day_Selections_And_Keep_Complete_Captured_Months(string key)
	{
		var d = Design(key); var now = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
		Should.Throw<BusinessException>(() => HrReportingEngine.ResolveParameters(d, new() { Period = "today" }, now, "Asia/Kolkata"));
		var resolved = HrReportingEngine.ResolveParameters(d, new() { Period = "month", Month = "2028-02" }, now, "Asia/Kolkata");
		resolved.FromDate.ShouldBe(new DateOnly(2028, 2, 1)); resolved.ToDate.ShouldBe(new DateOnly(2028, 2, 29));
		resolved.Period = "custom";
		HrReportingEngine.ResolveParameters(d, resolved, now, "Asia/Kolkata").ToDate.ShouldBe(new DateOnly(2028, 2, 29));
		d.DefaultPeriod = "today"; Should.Throw<BusinessException>(() => HrReportingEngine.Validate(d));
	}
	[Fact]
	public void Salary_Statement_Should_Capture_Whole_Month_Boundaries_For_Its_Saved_Evidence()
	{
		var p = HrReportingEngine.ResolveParameters(Design("salary-statement"), new() { Period = "custom", FromDate = new(2026, 8, 12), ToDate = new(2026, 10, 3), EmployeeIds = [Guid.NewGuid()] }, DateTimeOffset.UtcNow, "Asia/Kolkata");
		p.FromDate.ShouldBe(new DateOnly(2026, 8, 1)); p.ToDate.ShouldBe(new DateOnly(2026, 10, 31));
	}
	[Fact]
	public void Average_And_Distinct_Count_Should_Use_All_Matching_Source_Rows()
	{
		var d = Design("department-payroll");
		d.Summaries = [new() { Field = "netPay", Function = "avg" }, new() { Field = "employeeCode", Function = "distinct" }];
		var result = HrReportingEngine.Execute(d, new(), [
			new() { ["department"] = "Teaching", ["employeeCode"] = "A", ["netPay"] = 10m },
			new() { ["department"] = "Teaching", ["employeeCode"] = "A", ["netPay"] = 20m },
			new() { ["department"] = "Teaching", ["employeeCode"] = "B", ["netPay"] = 60m }]);
		result.Rows.Single()["avg_netPay"].ShouldBe(30m); result.Rows.Single()["distinct_employeeCode"].ShouldBe(2m);
	}
	[Fact]
	public void Nested_Any_Conditions_Should_Still_Respect_The_Outer_All_Group()
	{
		var d = Design("staff-directory");
		d.Filters = new()
		{
			Match = "all",
			Conditions = [new() { Field = "status", Operator = "equals", Value = "Active" }],
			Groups = [new() { Match = "any", Conditions = [new() { Field = "department", Operator = "equals", Value = "A" }, new() { Field = "department", Operator = "equals", Value = "B" }] }]
		};
		var result = HrReportingEngine.Execute(d, new(), [new() { ["status"] = "Active", ["department"] = "A" }, new() { ["status"] = "Inactive", ["department"] = "A" }, new() { ["status"] = "Active", ["department"] = "C" }]);
		result.Rows.Count.ShouldBe(1);
	}
	[Theory]
	[InlineData("between", "5", "10", 8, true)]
	[InlineData("between", "5", "10", 12, false)]
	[InlineData("gte", "5", "", 12, true)]
	public void Numeric_Conditions_Should_Compare_Numbers(string op, string first, string second, int actual, bool expected)
	{
		var d = Design("leave-requests");
		d.Filters.Conditions = [new() { Field = "requestNo", Operator = op, Value = first, Values = [first, second] }];
		HrReportingEngine.Execute(d, new(), [new() { ["requestNo"] = actual }]).Rows.Count.ShouldBe(expected ? 1 : 0);
	}
	[Fact]
	public void Missing_Value_Should_Remain_Null_And_Should_Not_Become_Zero()
	{
		var d = Design("salary-register");
		var result = HrReportingEngine.Execute(d, new(), [new() { ["employeeCode"] = "A", ["netPay"] = null }]);
		result.Rows.Single()["netPay"].ShouldBeNull(); result.Totals["netPay"].ShouldBeNull();
	}
	[Fact]
	public void Matrix_Should_Use_Iso_Date_Keys_And_Keep_Exception_Counts_Separate()
	{
		var d = Design("monthly-attendance"); var id = Guid.NewGuid();
		var result = HrReportingEngine.Execute(d, new() { FromDate = new(2026, 9, 1), ToDate = new(2026, 9, 30) }, [new() {
			["employeeId"] = id.ToString(), ["employeeCode"] = "A", ["attendanceDate"] = new DateOnly(2026, 9, 1), ["classification"] = "Present", ["exception"] = "Missing punch" }]);
		result.Rows.Single()["2026-09-01"].ShouldBe("Present"); result.Rows.Single()["Missing punch"].ShouldBe(1);
	}
	[Fact]
	public void Unsupported_Parameter_And_Deep_Filter_Groups_Should_Be_Rejected()
	{
		var d = Design("staff-directory");
		Should.Throw<BusinessException>(() => HrReportingEngine.ResolveParameters(d, new() { IncludeDrafts = true }, DateTimeOffset.UtcNow, "Asia/Kolkata"));
		d.Filters.Groups = [new() { Groups = [new()] }]; Should.Throw<BusinessException>(() => HrReportingEngine.Validate(d));
	}
	[Fact]
	public void Published_School_Definition_Should_Remain_Fixed_When_A_New_Draft_Is_Saved()
	{
		var template = new HrReportTemplate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", "", "Staff", "employee", "organization", "Draft", "{}");
		template.SaveDesign("Published", "", "Staff", "employee", "organization", "{\"version\":2}", true);
		template.SaveDesign("Draft", "", "Staff", "employee", "organization", "{\"version\":3}", false);
		template.Revision.ShouldBe(2); template.PublishedRevision.ShouldBe(1); template.PublishedName.ShouldBe("Published");
		template.PublishedDefinitionJson.ShouldBe("{\"version\":2}");
	}
	[Fact]
	public void Cancelled_Run_Should_Not_Be_Completed_By_An_Older_Worker()
	{
		var run = NewRun(); run.Start(run.AttemptId, DateTimeOffset.UtcNow).ShouldBeTrue(); run.Cancel();
		Should.Throw<BusinessException>(() => run.Complete(run.AttemptId, 1, "{}"));
		run.Fail(run.AttemptId); run.Status.ShouldBe("Cancelled");
	}
	[Fact]
	public void Generation_Should_Resolve_Only_Declared_Filter_Values_And_Keep_Fixed_Rules()
	{
		var d = Design("staff-directory");
		d.Filters.Conditions = [new() { Field = "status", Operator = "equals", Value = "Active" }, new() { Field = "department", Operator = "equals", AskWhenGenerating = true }];
		var before = System.Text.Json.JsonSerializer.Serialize(d);
		var p = new HrReportParametersDto { FilterValues = new() { ["filters.1"] = new() { Value = "Teaching" } } };
		var result = HrReportingEngine.Execute(d, p, [new() { ["status"] = "Active", ["department"] = "Teaching" }, new() { ["status"] = "Inactive", ["department"] = "Teaching" }, new() { ["status"] = "Active", ["department"] = "Office" }]);
		result.Rows.Count.ShouldBe(1); System.Text.Json.JsonSerializer.Serialize(d).ShouldBe(before);
		p.FilterValues["filters.0"] = new() { Value = "Inactive" };
		Should.Throw<BusinessException>(() => HrReportingEngine.Execute(d, p, []));
	}
	[Fact]
	public void Overnight_Shifts_Should_Remain_Open_Until_Their_Local_End_Time()
	{
		var date = new DateOnly(2026, 9, 1);
		HrReportingAttendanceDay.State(date, new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero), "Asia/Kolkata", new(22, 0), new(6, 0)).ShouldBe("Open");
		HrReportingAttendanceDay.State(date, new DateTimeOffset(2026, 9, 2, 1, 0, 0, TimeSpan.Zero), "Asia/Kolkata", new(22, 0), new(6, 0)).ShouldBe("Closed");
		HrReportingAttendanceDay.State(new(2026, 9, 3), new DateTimeOffset(2026, 9, 2, 1, 0, 0, TimeSpan.Zero), "Asia/Kolkata", null, null).ShouldBe("Future");
	}
	[Fact]
	public void Salary_Components_Should_Keep_Distinct_Identities_Precision_And_Unavailable_Values()
	{
		var d = Design("salary-components");
		var result = HrReportingEngine.Execute(d, new(), [
			new() { ["employeeCode"] = "A", ["__components"] = new List<HrReportingSavedComponent> { new("Earning:1", "Earning: Allowance", 12.34m, true), new("Earning:2", "Earning: Allowance", 56.78m, true) } },
			new() { ["employeeCode"] = "B", ["__components"] = new List<HrReportingSavedComponent> { new("Earning:1", "Earning: Renamed allowance", 0m, true) } }]);
		result.Columns.Count(x => x.Id.StartsWith("Earning:")).ShouldBe(2);
		result.Rows[0]["Earning:1"].ShouldBe(12.34m); result.Rows[1]["Earning:1"].ShouldBe(0m);
		result.Rows[1]["Earning:2"].ShouldBeNull(); result.Totals["Earning:2"].ShouldBe(56.78m);
	}
	[Fact]
	public void Interrupted_Work_Should_Be_Retryable_Without_A_Duplicate_Completion()
	{
		var run = NewRun(); var start = DateTimeOffset.UtcNow;
		run.Start(run.AttemptId, start).ShouldBeTrue(); run.Start(run.AttemptId, start).ShouldBeFalse();
		run.RecoverInterruptedWork(start.AddMinutes(31)); run.Status.ShouldBe("Failed");
		Should.Throw<BusinessException>(() => run.Complete(run.AttemptId, 1, "{}"));
	}
	[Fact]
	public void Excel_Should_Preserve_Typed_Numbers_Dates_And_Literal_Text()
	{
		var run = NewRun(); var result = new HrReportingResult
		{
			SchoolName = "School",
			Columns = [HrReportingCatalogue.Field("net", "Net", "money"), HrReportingCatalogue.Field("day", "Day", "date"), HrReportingCatalogue.Field("name", "Name", "string")],
			Rows = [new() { ["net"] = 123.45m, ["day"] = new DateOnly(2026, 9, 1), ["name"] = "=SUM(A1:A2)" }]
		};
		using var stream = new MemoryStream(HrReportingDocumentWriter.Excel(run, result)); using var workbook = new XLWorkbook(stream);
		var sheet = workbook.Worksheet("Report"); sheet.Cell(6, 1).GetValue<decimal>().ShouldBe(123.45m);
		sheet.Cell(6, 2).GetDateTime().Date.ShouldBe(new DateTime(2026, 9, 1)); sheet.Cell(6, 3).HasFormula.ShouldBeFalse();
	}
	private static HrReportRun NewRun() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Report", "builtin:test", 1, "employee", "{}", "{}", "{}", DateTimeOffset.UtcNow);
}
