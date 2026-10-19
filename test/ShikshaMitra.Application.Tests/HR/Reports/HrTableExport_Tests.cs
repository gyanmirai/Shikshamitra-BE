using System;
using System.Text.Json;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrTableExport_Tests
{
	private static HrTableExportRequest Request(string table = "employee-table", string field = "fullName") => new() {
		Table = table, Query = JsonSerializer.SerializeToElement(new { }), Columns = [new() { Id = field, Label = "Staff" }]
	};
	[Theory]
	[InlineData("sql", "fullName")]
	[InlineData("employee-table", "bankAccountNumber")]
	[InlineData("employee-table", "actions")]
	[InlineData("hr-attendance-register", "select")]
	public void Export_Should_Reject_Unknown_Sources_And_Non_Data_Columns(string table, string field) =>
		Should.Throw<BusinessException>(() => HrTableExportSources.Validate(Request(table, field)));
	[Fact]
	public void Export_Should_Reject_Duplicated_Or_Empty_Columns_And_Unsupported_Formats()
	{
		var request = Request(); request.Columns.Add(request.Columns[0]);
		Should.Throw<BusinessException>(() => HrTableExportSources.Validate(request));
		request = Request(); request.Columns.Clear(); Should.Throw<BusinessException>(() => HrTableExportSources.Validate(request));
		request = Request(); request.Format = "csv"; Should.Throw<BusinessException>(() => HrTableExportSources.Validate(request));
	}
	[Theory]
	[InlineData(0, "Active")]
	[InlineData(1, "Inactive")]
	[InlineData(2, "Left")]
	[InlineData(3, "Retired")]
	public void Employee_Status_Should_Match_The_Directory(int status, string label)
	{
		var row = JsonSerializer.SerializeToElement(new { status, doj = "2020-01-01" });
		HrTableExportProjection.Value("employee-table", "statusValue", row, new(2026, 10, 19)).ShouldBe(label);
	}
	[Fact]
	public void Future_Joining_And_Manual_Capture_Exceptions_Should_Keep_Their_Display_Meaning()
	{
		var staff = JsonSerializer.SerializeToElement(new { status = 0, doj = "2026-11-01" });
		HrTableExportProjection.Value("employee-table", "statusValue", staff, new(2026, 10, 19)).ShouldBe("Upcoming");
		var attendance = JsonSerializer.SerializeToElement(new { absent = 2, halfDay = 1, missingPunch = 0, editable = 3, locked = 7 });
		HrTableExportProjection.Value("hr-attendance-register", "exceptions", attendance).ShouldBe("Absent: 2; half day: 1; missing punch: 0");
		HrTableExportProjection.Value("hr-attendance-register", "editability", attendance).ShouldBe("Editable: 3; locked: 7");
	}
	[Fact]
	public void Export_Types_Should_Keep_Money_Dates_And_Counts_Distinct()
	{
		HrTableExportProjection.Field(new() { Id = "netSalary", Label = "Net pay" }).Type.ShouldBe("money");
		HrTableExportProjection.Field(new() { Id = "present", Label = "Present" }).Type.ShouldBe("number");
		HrTableExportProjection.Field(new() { Id = "dob", Label = "Birth date" }).Type.ShouldBe("date");
		HrTableExportProjection.Field(new() { Id = "submittedAt", Label = "Submitted" }).Type.ShouldBe("datetime");
	}
}
