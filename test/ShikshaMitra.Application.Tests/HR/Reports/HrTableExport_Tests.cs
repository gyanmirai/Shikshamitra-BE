using System;
using System.Text.Json;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrTableExport_Tests
{
	[Fact]
	public async Task Captured_Export_Access_Checks_Should_Validate_Metadata_Without_Reading_Population()
	{
		var id = Guid.NewGuid();
		var run = new HrReportRunDto { Id = id, Status = "Completed", Columns = [new() { Id = "netPay", Type = "money" }] };
		var workspace = Substitute.For<IHrReportingWorkspaceAppService>();
		workspace.GetAsync(id).Returns(run);
		var services = Substitute.For<IAbpLazyServiceProvider>();
		services.LazyGetRequiredService<IHrReportingWorkspaceAppService>().Returns(workspace);
		var source = new HrTableExportSources(services);
		var request = Request("hr-generated-report", "netPay");
		request.Query = JsonSerializer.SerializeToElement(new { runId = id, criteria = new { filters = new[] { new { column = "netPay", type = "greaterThan", value = 0 } } } });
		await source.EnsureAccessAsync(request);
		await workspace.Received(1).GetAsync(id);
		workspace.ReceivedCalls().ShouldNotContain(call => call.GetMethodInfo().Name == "GetRowsAsync" || call.GetMethodInfo().Name == "GetExportRowsAsync");
		run.IsExpired = true;
		await Should.ThrowAsync<BusinessException>(() => source.EnsureAccessAsync(request));
		run.IsExpired = false;
		run.Status = "Queued";
		await Should.ThrowAsync<BusinessException>(() => source.EnsureAccessAsync(request));
	}
	[Fact]
	public async System.Threading.Tasks.Task Filtered_Captured_Exports_Should_Omit_Unfiltered_Totals()
	{
		var source = new HrTableExportSources(null!);
		var request = Request("hr-generated-report", "netPay");
		request.Query = JsonSerializer.SerializeToElement(new { criteria = new { search = "staff" } });
		(await source.TotalsAsync(request, new())).ShouldBeEmpty();
		request.Query = JsonSerializer.SerializeToElement(new { criteria = new { filters = new[] { new { column = "netPay", type = "greaterThan", value = 0 } } } });
		(await source.TotalsAsync(request, new())).ShouldBeEmpty();
	}
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
		var attendance = JsonSerializer.SerializeToElement(new { absent = 2, halfDay = 1, missingPunch = 0, editable = 3, locked = 7, hasExceptions = true });
		HrTableExportProjection.Value("hr-attendance-register", "exceptions", attendance).ShouldBe("Absent 2; Half day 1; Locked 7");
		HrTableExportProjection.Value("hr-attendance-register", "editability", attendance).ShouldBe("3 editable; 7 locked");
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
