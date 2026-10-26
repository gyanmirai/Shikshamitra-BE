using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Search;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.HR.Reports;

public class HrOperationalTableQuery_UnitTests(ITestOutputHelper output)
{
	[Theory]
	[InlineData("{\"statusCode\":0,\"status\":\"Absent\"}", "Present")]
	[InlineData("{\"statusCode\":1,\"status\":\"Present\"}", "Absent")]
	[InlineData("{\"statusCode\":2,\"status\":null}", "HalfDay")]
	[InlineData("{\"statusCode\":null,\"status\":\" full_day-leave \"}", "Absent")]
	[InlineData("{\"status\":\"Half Day Leave\"}", "HalfDay")]
	[InlineData("{\"status\":\"unknown\"}", "Pending")]
	public void Imported_Attendance_Query_And_Export_Should_Match_The_Editor_Status(string json, string expected)
	{
		var row = JsonDocument.Parse(json).RootElement;
		var field = new HrReportFieldDto { Id = "attendanceStatus", Type = "enum" };
		var value = HrTableExportProjection.QueryValue("hr-attendance-import-rows", field, row, row, new(2026, 10, 26));
		value.ShouldBe(expected);
		var rows = new[] { new Dictionary<string, object?> { [field.Id] = value } };
		new HrReportingTableQuery(new() { Filters = [Filter(field.Id, "equals", expected)] }, [field], "Asia/Kolkata").Apply(rows, item => item).Count().ShouldBe(1);
		new HrReportingTableQuery(new() { Filters = [Filter(field.Id, "equals", expected == "Present" ? "Absent" : "Present")] }, [field], "Asia/Kolkata").Apply(rows, item => item).ShouldBeEmpty();
		HrTableExportProjection.Value("hr-attendance-import-rows", field.Id, row).ShouldBe(expected);
	}
	[Theory]
	[InlineData("employee-onboarding-table", "candidate", "{\"fullName\":\"Zulu\",\"email\":\"unique@example.test\",\"mobileNo\":\"hidden-phone\"}", "unique@example.test", "Zulu")]
	[InlineData("employee-onboarding-table", "candidate", "{\"fullName\":\"Zulu\",\"email\":null,\"mobileNo\":\"unique-phone\"}", "unique-phone", "Zulu")]
	[InlineData("employee-onboarding-table", "candidate", "{\"fullName\":\"Zulu\",\"applicationNumber\":\"unique-application\"}", "unique-application", "Zulu")]
	[InlineData("employee-onboarding-table", "position", "{\"positionAppliedFor\":\"Teacher\",\"departmentAppliedFor\":\"unique-department\"}", "unique-department", "Teacher")]
	[InlineData("hr-attendance-register", "employeeName", "{\"employeeName\":\"Zulu\",\"employeeCode\":\"unique-code\",\"educationMediumStageName\":\"unique-division\"}", "unique-division", "Zulu")]
	[InlineData("hr-manual-attendance", "fullName", "{\"fullName\":\"Zulu\",\"employeeCode\":\"unique-code\"}", "unique-code", "Zulu")]
	[InlineData("payroll-monthly-workbench-table", "employeeName", "{\"employeeName\":\"Zulu\",\"employeeIdentifier\":\"unique-code\",\"employeeGroup\":\"Non-teaching\"}", "Non-teaching", "Zulu")]
	[InlineData("esi-employee-impact", "employeeName", "{\"employeeName\":\"Zulu\",\"employeeIdentifier\":\"unique-code\"}", "unique-code", "Zulu")]
	[InlineData("hr-leave-requests", "name", "{\"name\":\"Zulu\",\"employeeIdentifier\":\"unique-code\"}", "unique-code", "Zulu")]
	[InlineData("hr-self-leave-history", "name", "{\"name\":\"Zulu\",\"employeeIdentifier\":\"unique-code\"}", "unique-code", "Zulu")]
	[InlineData("hr-policy-calendar", "effective", "{\"effectiveFrom\":\"2026-06-01\",\"effectiveTo\":null}", "01 Jun 2026 - No end date", "2026-06-01")]
	[InlineData("hr-policy-assignments", "effective", "{\"effectiveFrom\":\"2026-06-01\",\"effectiveTo\":\"2027-05-31\"}", "01 Jun 2026 - 31 May 2027", "2026-06-01")]
	public void Composite_Visible_Text_Should_Filter_Search_And_Export_Without_Changing_Primary_Order(string table, string column, string json, string visible, string primary)
	{
		var row = JsonDocument.Parse(json).RootElement;
		var field = new HrReportFieldDto { Id = column, Type = "string" };
		var projected = HrTableExportProjection.QueryValue(table, field, row, row, new(2026, 10, 26));
		projected.ShouldBeOfType<HrOperationalTextValue>().Order.ShouldBe(primary);
		var rows = new[] { new Dictionary<string, object?> { [column] = projected } };
		new HrReportingTableQuery(new() { Filters = [Filter(column, "contains", visible)] }, [field], "Asia/Kolkata").Apply(rows, item => item).Count().ShouldBe(1);
		new HrReportingTableQuery(new() { Search = visible }, [field], "Asia/Kolkata").Apply(rows, item => item).Count().ShouldBe(1);
		Convert.ToString(HrTableExportProjection.Value(table, column, row)).ShouldContain(visible);
		new HrReportingTableQuery(new() { Search = "hidden-phone" }, [field], "Asia/Kolkata").Apply(rows, item => item).ShouldBeEmpty();
	}
	[Theory]
	[InlineData("{\"weeklyOffPattern\":\"custom\",\"customWeeklyOffs\":[6,1,1]}", "Custom: Mon, Sat")]
	[InlineData("{\"weeklyOffPattern\":\"sunday_only\"}", "Sunday Off")]
	[InlineData("{\"weeklyOffPattern\":\"\"}", "Saturday + Sunday Off")]
	public void Calendar_Weekly_Rules_Should_Use_Visible_Labels(string json, string expected)
	{
		HrTableExportProjection.Value("hr-policy-calendar", "weeklyRule", JsonDocument.Parse(json).RootElement).ShouldBe(expected);
	}
	[Fact]
	public void Performance_Sections_Should_Include_All_Displayed_Domain_Labels()
	{
		var row = JsonSerializer.SerializeToElement(new { sections = new[] { new { name = "Section", weight = 25, sqaaDomain = 3 } } });
		HrTableExportProjection.Value("hr-performance-templates", "sections", row).ShouldBe("Section: 25% Leadership & Governance");
	}
	[Fact]
	public void Formatted_Range_Sorting_Should_Remain_Chronological_In_Both_Directions()
	{
		var field = new HrReportFieldDto { Id = "effective", Type = "string" };
		var rows = new[] { "2026-12-01", "2026-02-20", "2025-10-26" }.Select(from => {
			var row = JsonSerializer.SerializeToElement(new { effectiveFrom = from });
			return new Dictionary<string, object?> { [field.Id] = HrTableExportProjection.QueryValue("hr-policy-calendar", field, row, row, new(2026, 10, 26)) };
		}).ToList();
		foreach (var direction in new[] { "asc", "desc" }) {
			var sorted = new HrReportingTableQuery(new() { Sorts = [new() { Column = field.Id, Direction = direction }] }, [field], "Asia/Kolkata").Apply(rows, item => item).Select(item => ((HrOperationalTextValue)item[field.Id]!).Order).ToList();
			sorted.ShouldBe(direction == "asc" ? new object?[] { "2025-10-26", "2026-02-20", "2026-12-01" } : new object?[] { "2026-12-01", "2026-02-20", "2025-10-26" });
		}
	}
	[Fact]
	public void Register_Exception_And_Editability_Queries_Should_Only_Match_Visible_Positive_Labels()
	{
		var values = new[] { new { absent = 0, halfDay = 0, missingPunch = 0, locked = 1, editable = 0, hasExceptions = true }, new { absent = 1, halfDay = 0, missingPunch = 0, locked = 0, editable = 1, hasExceptions = true }, new { absent = 0, halfDay = 0, missingPunch = 0, locked = 0, editable = 1, hasExceptions = false } };
		var rows = values.Select(value => JsonSerializer.SerializeToElement(value)).ToList();
		var projected = rows.Select(row => new Dictionary<string, object?> { ["exceptions"] = HrTableExportProjection.Value("hr-attendance-register", "exceptions", row), ["editability"] = HrTableExportProjection.Value("hr-attendance-register", "editability", row) }).ToList();
		projected.Select(row => row["exceptions"]).ShouldBe(new object?[] { "Locked 1", "Absent 1", "Clear" });
		List<HrReportFieldDto> fields = [new() { Id = "exceptions", Type = "string" }, new() { Id = "editability", Type = "string" }];
		foreach (var label in new[] { "Absent", "Locked", "Clear" })
			new HrReportingTableQuery(new() { Filters = [Filter("exceptions", "contains", label)] }, fields, "Asia/Kolkata").Apply(projected, row => row).Count().ShouldBe(1);
		new HrReportingTableQuery(new() { Filters = [Filter("editability", "contains", "locked")] }, fields, "Asia/Kolkata").Apply(projected, row => row).Count().ShouldBe(1);
	}
	[Theory]
	[InlineData("employee-table")]
	[InlineData("employee-icard-selection-table")]
	public void Visible_Staff_Identifiers_Should_Filter_And_Export_With_Names_While_Sorting_By_Name(string table)
	{
		var rows = new[] { new { fullName = "Zulu", employeeIdentifier = "unique-001" }, new { fullName = "Alpha", employeeIdentifier = "unique-999" } }.Select(row => JsonSerializer.SerializeToElement(row)).ToList();
		var field = new HrReportFieldDto { Id = "fullName", Type = "string" };
		var projected = rows.Select(row => new Dictionary<string, object?> { ["fullName"] = HrTableExportProjection.QueryValue(table, field, row, row, new(2026, 10, 26)) }).ToList();
		new HrReportingTableQuery(new() { Filters = [Filter("fullName", "contains", "unique-001")] }, [field], "Asia/Kolkata").Apply(projected, row => row).Count().ShouldBe(1);
		new HrReportingTableQuery(new() { Search = "unique-001" }, [field], "Asia/Kolkata").Apply(projected, row => row).Count().ShouldBe(1);
		new HrReportingTableQuery(new() { Sorts = [new() { Column = "fullName" }] }, [field], "Asia/Kolkata").Apply(projected, row => row).First()["fullName"].ShouldBeOfType<HrOperationalTextValue>().Order.ShouldBe("Alpha");
		HrTableExportProjection.Value(table, "fullName", rows[0]).ShouldBe("Zulu unique-001");
	}
	[Theory]
	[InlineData("hr-earning-components", "earningType", "{\"componentType\":\"Fixed\"}", "{\"componentType\":0}", "0")]
	[InlineData("hr-earning-components", "visibleInPayslip", "{\"visibleInPayslip\":false}", "{\"visibleInPayslip\":false}", "false")]
	[InlineData("employee-onboarding-table", "status", "{\"status\":\"InvitationSent\"}", "{\"status\":0}", "0")]
	[InlineData("hr-pf-rules", "isActive", "{\"isActive\":false}", "{\"isActive\":false}", "false")]
	public async Task Enum_Select_Values_Should_Match_Native_Codes_And_Readable_Labels(string table, string id, string display, string raw, string selected)
	{
		var (source, _) = Source();
		var fields = await source.OperationalColumnsAsync(new() { Table = table });
		var field = fields.Single(x => x.Id == id);
		field.Type.ShouldBe("enum");
		var value = HrTableExportProjection.QueryValue(table, field, JsonDocument.Parse(display).RootElement, JsonDocument.Parse(raw).RootElement, new(2026, 10, 26));
		var rows = new[] { new Dictionary<string, object?> { [id] = value } };
		new HrReportingTableQuery(new() { Filters = [new() { Column = id, Type = "equals", Value = JsonDocument.Parse(selected).RootElement.Clone() }] }, fields, "Asia/Kolkata").Apply(rows, row => row).Count().ShouldBe(1);
		new HrReportingTableQuery(new() { Filters = [Filter(id, "equals", ((HrOperationalEnumValue)value!).Display!)] }, fields, "Asia/Kolkata").Apply(rows, row => row).Count().ShouldBe(1);
	}
	[Fact]
	public async Task ICard_Selection_Should_Query_All_Authorized_Staff_And_Computed_Readiness_Before_Paging()
	{
		var rows = Enumerable.Range(0, 125).Select(index => new EmployeeSummary { Id = Guid.NewGuid(), FullName = $"Staff {index:000}", Designation = index % 2 == 0 ? "Teacher" : "Clerk", MobileNo = "synthetic", ThumbnailPicture = "synthetic", EmployeeIdentifier = index > 25 ? null : "synthetic" }).ToList();
		var reader = Substitute.For<IEmployeeSearchAppService>();
		reader.GetFilteredListAsync(Arg.Any<GetEmployeeListInput>()).Returns(call => { var input = call.Arg<GetEmployeeListInput>(); return new EmployeeListResponse { TotalCount = rows.Count, Items = rows.Skip(input.SkipCount).Take(input.MaxResultCount).ToList() }; });
		var services = Substitute.For<IAbpLazyServiceProvider>(); services.LazyGetRequiredService<IEmployeeSearchAppService>().Returns(reader);
		var settings = Substitute.For<ISettingProvider>(); settings.GetOrNullAsync(Arg.Any<string>()).Returns("Asia/Kolkata"); services.LazyGetRequiredService<HrReportingTimeZone>().Returns(new HrReportingTimeZone(settings));
		var cancellation = Substitute.For<ICancellationTokenProvider>(); cancellation.Token.Returns(CancellationToken.None); services.LazyGetRequiredService<ICancellationTokenProvider>().Returns(cancellation);
		var source = new HrTableExportSources(services);
		var request = new HrTableExportRequest { Table = "employee-icard-selection-table", Columns = [new() { Id = "fullName", Label = "Staff" }], Query = JsonSerializer.SerializeToElement(new { mediumStageId = Guid.NewGuid(), designation = "Teacher", criteria = new HrReportTableQueryDto { Filters = [Filter("readiness", "contains", "Code"), Filter("designation", "equals", "Teacher")], Sorts = [new() { Column = "fullName", Direction = "desc" }] } }, HrReportingEngine.Json) };
		var page = await source.ReadAsync(request, 0, 25);
		page.Total.ShouldBe(50); page.Rows[0].GetProperty("fullName").GetString().ShouldBe("Staff 124"); page.Rows.Count.ShouldBe(25);
		var export = await source.ReadAsync(request, 25, 25); export.Total.ShouldBe(50); export.Rows[0].GetProperty("fullName").GetString().ShouldBe("Staff 074");
		await reader.Received(2).GetFilteredListAsync(Arg.Is<GetEmployeeListInput>(input => input.MediumStageId.HasValue && input.Designation == "Teacher" && input.MaxResultCount == 100));
	}
	private static HrReportTableFilterDto Filter(string column, string type, object value) => new() { Column = column, Type = type, Value = JsonSerializer.SerializeToElement(value) };
	private static (HrTableExportSources Source, IEmployeeApplicationAppService Reader) Source(bool incomplete = false, int count = 251, bool changing = false, long? reportedTotal = null)
	{
		var data = Enumerable.Range(0, count).Select(index => new EmployeeApplicationListItemDto {
			Id = Guid.NewGuid(), FullName = $"Candidate {index:000}", PositionAppliedFor = index % 2 == 0 ? "Teacher" : "Accounts",
			Email = $"synthetic-{index:000}@example.test",
			SubmittedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index), Status = (EmployeeApplicationStatus)0
		}).ToList();
		var reader = Substitute.For<IEmployeeApplicationAppService>();
		reader.GetListAsync(Arg.Any<GetEmployeeApplicationListInput>()).Returns(call => {
			var input = call.Arg<GetEmployeeApplicationListInput>();
			return new PagedResultDto<EmployeeApplicationListItemDto>(reportedTotal ?? data.Count + (changing && input.SkipCount > 0 ? 1 : 0),
				incomplete && input.SkipCount > 0 ? [] : data.Skip(input.SkipCount).Take(input.MaxResultCount).ToList());
		});
		var services = Substitute.For<IAbpLazyServiceProvider>();
		services.LazyGetRequiredService<IEmployeeApplicationAppService>().Returns(reader);
		var settings = Substitute.For<ISettingProvider>();
		settings.GetOrNullAsync(Arg.Any<string>()).Returns("Asia/Kolkata");
		services.LazyGetRequiredService<HrReportingTimeZone>().Returns(new HrReportingTimeZone(settings));
		var cancellation = Substitute.For<ICancellationTokenProvider>();
		cancellation.Token.Returns(CancellationToken.None);
		services.LazyGetRequiredService<ICancellationTokenProvider>().Returns(cancellation);
		return (new HrTableExportSources(services), reader);
	}
	private static HrTableExportRequest Request(HrReportTableQueryDto criteria) => new() {
		Table = "employee-onboarding-table", Columns = [new() { Id = "candidate", Label = "Candidate" }],
		Query = JsonSerializer.SerializeToElement(new { status = 0, search = "owning-reader-scope", criteria }, HrReportingEngine.Json)
	};
	[Fact]
	public async Task Visible_Off_Page_Contact_Should_Use_Global_Criteria_Count_And_Export()
	{
		var (source, reader) = Source();
		var request = Request(new() { Filters = [Filter("candidate", "contains", "synthetic-220@example.test"), Filter("position", "equals", "Teacher")], Sorts = [new() { Column = "candidate", Direction = "desc" }] });
		var page = await source.ReadAsync(request, 0, 10);
		page.Total.ShouldBe(1);
		page.Rows.Single().GetProperty("fullName").GetString().ShouldBe("Candidate 220");
		var export = await source.ReadAsync(request, 0, 100);
		export.Rows.Single().GetRawText().ShouldBe(page.Rows.Single().GetRawText());
		HrTableExportProjection.Value(request.Table, "candidate", export.Rows.Single()).ShouldBe("Candidate 220 synthetic-220@example.test");
		await reader.Received(3).GetListAsync(Arg.Is<GetEmployeeApplicationListInput>(input => input.Search == "owning-reader-scope" && input.MaxResultCount == 100));
	}
	[Fact]
	public async Task Full_Result_Criteria_And_Export_Should_Share_Authorized_Reader_Pages_And_Raw_Enums()
	{
		var (source, reader) = Source();
		var request = Request(new() { Filters = [Filter("candidate", "startsWith", "Candidate 2"), Filter("position", "equals", "Teacher")], Sorts = [new() { Column = "submittedAt", Direction = "desc" }] });
		var first = await source.ReadAsync(request, 0, 10);
		first.Total.ShouldBe(26); first.Rows.Count.ShouldBe(10);
		first.Rows[0].GetProperty("fullName").GetString().ShouldBe("Candidate 250");
		first.Envelope.GetProperty("items")[0].GetProperty("status").ValueKind.ShouldBe(JsonValueKind.Number);
		var second = await source.ReadAsync(request, 10, 10);
		second.Rows[0].GetProperty("fullName").GetString().ShouldBe("Candidate 230");
		var export = await source.ReadAsync(request, 0, 100);
		export.Rows.Count.ShouldBe(26);
		export.Rows.Take(10).Select(row => row.GetRawText()).ShouldBe(first.Rows.Select(row => row.GetRawText()));
		await reader.Received(3).GetListAsync(Arg.Is<GetEmployeeApplicationListInput>(input => input.Search == "owning-reader-scope" && (int?)input.Status == 0 && input.MaxResultCount == 100));
	}
	[Fact]
	public async Task Incomplete_Populations_Should_Fail_Instead_Of_Returning_A_Page_Local_Answer()
	{
		var (source, _) = Source(incomplete: true);
		await Should.ThrowAsync<BusinessException>(() => source.ReadAsync(Request(new() { Sorts = [new() { Column = "candidate" }] }), 0, 10));
	}
	[Theory]
	[InlineData(10000)]
	[InlineData(25000)]
	public async Task Larger_Synthetic_Captures_Should_Have_Bounded_Reader_Calls_And_Reuse_Export_Population(int count)
	{
		var (source, reader) = Source(count: count);
		var request = Request(new() { Filters = [Filter("position", "equals", "Teacher")], Sorts = [new() { Column = "submittedAt", Direction = "desc" }] });
		var before = GC.GetTotalAllocatedBytes();
		var timer = Stopwatch.StartNew();
		var page = await source.ReadAsync(request, 10, 10);
		timer.Stop();
		var allocated = GC.GetTotalAllocatedBytes() - before;
		page.Total.ShouldBe(count / 2); page.Rows.Count.ShouldBe(10);
		page.Rows[0].GetProperty("fullName").GetString().ShouldBe($"Candidate {count - 22:000}");
		var calls = (int)Math.Ceiling(count / 100d);
		await reader.Received(calls).GetListAsync(Arg.Any<GetEmployeeApplicationListInput>());
		var export = await source.ReadAsync(request, 0, 100);
		export.Total.ShouldBe(page.Total);
		export.Rows.Skip(10).Take(10).Select(row => row.GetRawText()).ShouldBe(page.Rows.Select(row => row.GetRawText()));
		await reader.Received(calls).GetListAsync(Arg.Any<GetEmployeeApplicationListInput>());
		output.WriteLine($"Capture rows={count}; matches={page.Total}; sourceCalls={calls}; sourcePageSize=100; responseRows={page.Rows.Count}; elapsedMs={timer.ElapsedMilliseconds}; allocatedBytes={allocated}; configuredCap={HrReportingEngine.MaximumPopulation}");
	}
	[Fact]
	public async Task Changed_Or_Over_Cap_Populations_Should_Return_An_Error_Without_Partial_Results()
	{
		var request = Request(new() { Sorts = [new() { Column = "candidate" }] });
		var (changing, changedReader) = Source(changing: true);
		await Should.ThrowAsync<BusinessException>(() => changing.ReadAsync(request, 0, 10));
		await changedReader.Received(2).GetListAsync(Arg.Any<GetEmployeeApplicationListInput>());
		var (large, cappedReader) = Source(reportedTotal: HrReportingEngine.MaximumPopulation + 1L);
		var error = await Should.ThrowAsync<BusinessException>(() => large.ReadAsync(request, 0, 10));
		error.Code.ShouldBe(HRErrorCodes.ReportPopulationTooLarge);
		await cappedReader.Received(1).GetListAsync(Arg.Any<GetEmployeeApplicationListInput>());
	}
	[Fact]
	public async Task Esi_Capture_Should_Read_One_Authorized_Model_For_All_Source_Pages()
	{
		var rows = Enumerable.Range(0, 1000).Select(index => new EmployeeStatutoryImpactDto {
			EmployeeId = Guid.NewGuid(), EmployeeName = $"Synthetic {index:0000}", StatutoryWages = index + 0.5m,
			EnrollmentStatus = "Covered", IsCovered = true
		}).ToList();
		var reader = Substitute.For<IEsiStatutoryAppService>();
		reader.GetTablePopulationAsync(Arg.Any<StatutoryImpactPreviewRequestDto>()).Returns(new StatutoryImpactPreviewDto { Items = rows, TotalCount = rows.Count });
		var services = Substitute.For<IAbpLazyServiceProvider>();
		services.LazyGetRequiredService<IEsiStatutoryAppService>().Returns(reader);
		var settings = Substitute.For<ISettingProvider>(); settings.GetOrNullAsync(Arg.Any<string>()).Returns("Asia/Kolkata");
		services.LazyGetRequiredService<HrReportingTimeZone>().Returns(new HrReportingTimeZone(settings));
		var cancellation = Substitute.For<ICancellationTokenProvider>(); cancellation.Token.Returns(CancellationToken.None);
		services.LazyGetRequiredService<ICancellationTokenProvider>().Returns(cancellation);
		var source = new HrTableExportSources(services);
		var request = new HrTableExportRequest { Table = "esi-employee-impact", Columns = [new() { Id = "statutoryWages", Label = "Wages" }],
			Query = JsonSerializer.SerializeToElement(new { monthYear = "10-2026", criteria = new HrReportTableQueryDto {
				Filters = [Filter("statutoryWages", "greaterThan", 500m)], Sorts = [new() { Column = "statutoryWages", Direction = "desc" }] } }, HrReportingEngine.Json) };
		var first = await source.ReadAsync(request, 0, 10);
		first.Total.ShouldBe(500); first.Rows[0].GetProperty("statutoryWages").GetDecimal().ShouldBe(999.5m);
		(await source.ReadAsync(request, 100, 10)).Rows[0].GetProperty("statutoryWages").GetDecimal().ShouldBe(899.5m);
		await reader.Received(1).GetTablePopulationAsync(Arg.Any<StatutoryImpactPreviewRequestDto>());
	}
	[Theory]
	[InlineData("bankAccountNumber", "equals", "secret")]
	[InlineData("candidate", "between", "name")]
	[InlineData("submittedAt", "equals", "31-02-2026")]
	public async Task Unsupported_Fields_Operators_And_Dates_Should_Be_Rejected(string column, string operation, string value)
	{
		var (source, reader) = Source();
		await Should.ThrowAsync<BusinessException>(() => source.ReadAsync(Request(new() { Filters = [Filter(column, operation, value)] }), 0, 10));
		await reader.DidNotReceive().GetListAsync(Arg.Any<GetEmployeeApplicationListInput>());
	}
	[Fact]
	public void Unlimited_Amounts_Should_Sort_Above_Finite_Values_And_Never_Match_Finite_Filters()
	{
		List<HrReportFieldDto> fields = [new() { Id = "amount", Type = "money" }];
		var rows = new[] { new Dictionary<string, object?> { ["amount"] = "No ceiling" }, new() { ["amount"] = 2.5m }, new() { ["amount"] = 12m }, new() { ["amount"] = null } };
		var engine = new HrReportingTableQuery(new() { Sorts = [new() { Column = "amount" }] }, fields, "Asia/Kolkata");
		engine.Apply(rows, row => row).Select(row => row["amount"]).ShouldBe(new object?[] { null, 2.5m, 12m, "No ceiling" });
		engine = new(new() { Filters = [Filter("amount", "greaterThan", 1.25m)] }, fields, "Asia/Kolkata");
		engine.Apply(rows, row => row).Count().ShouldBe(2);
	}
	[Fact]
	public void Enum_Display_Separators_Should_Not_Change_Their_Identity()
	{
		var engine = new HrReportingTableQuery(new() { Filters = [Filter("group", "equals", "Non Teaching")] }, [new() { Id = "group", Type = "enum" }], "Asia/Kolkata");
		engine.Apply(new[] { "Non-teaching", "Teaching" }, value => new() { ["group"] = value }).ShouldBe(new[] { "Non-teaching" });
	}
	[Theory]
	[InlineData("09:30")]
	[InlineData("09:30:00")]
	[InlineData("9:30 AM")]
	public void Clock_Filters_Should_Accept_The_Display_And_Wire_Formats_Without_Losing_Chronological_Order(string value)
	{
		List<HrReportFieldDto> fields = [new() { Id = "at", Type = "clock" }];
		var rows = new[] { "13:00:00", "09:30:00", "08:00:00" };
		new HrReportingTableQuery(new() { Sorts = [new() { Column = "at" }] }, fields, "Asia/Kolkata")
			.Apply(rows, time => new() { ["at"] = time }).ShouldBe(new[] { "08:00:00", "09:30:00", "13:00:00" });
		new HrReportingTableQuery(new() { Filters = [Filter("at", "equals", value)] }, fields, "Asia/Kolkata")
			.Apply(rows, time => new() { ["at"] = time }).ShouldBe(new[] { "09:30:00" });
	}
	[Fact]
	public void Salary_Envelope_Should_Keep_Unavailable_Values_And_Display_Only_Selected_Page_Totals()
	{
		var original = JsonSerializer.SerializeToElement(new { rows = Array.Empty<object>(), totalCount = 4, page = 1, pageSize = 2, totals = new { netPay = 999 }, totalsLabel = "Totals for displayed rows" });
		var raw = JsonSerializer.SerializeToElement(new { employeeCode = "001", netPay = (string?)null });
		var missing = new HrOperationalTableRow(raw, raw, new() { ["employeeCode"] = "001", ["netPay"] = null });
		var available = missing with { Values = new() { ["employeeCode"] = "002", ["netPay"] = 10.25m } };
		var envelope = HrOperationalTableQuery.Envelope("hr-salary-documents", original, [missing, available], [missing, available, available], 0, 2,
			[new() { Id = "employeeCode" }, new() { Id = "netPay", Type = "number" }]);
		envelope.GetProperty("totalCount").GetInt32().ShouldBe(3);
		envelope.GetProperty("rows")[0].GetProperty("netPay").ValueKind.ShouldBe(JsonValueKind.Null);
		envelope.GetProperty("totals").GetProperty("netPay").GetDecimal().ShouldBe(10.25m);
		envelope.GetProperty("totals").TryGetProperty("employeeCode", out _).ShouldBeFalse();
	}
	[Fact]
	public void Payroll_And_Manual_Attendance_Projections_Should_Match_Visible_Record_Meaning()
	{
		var payroll = JsonSerializer.SerializeToElement(new { netPay = 120.5m, isLocked = true, reviewStatus = "PENDING", issueCodes = new[] { "MISSING_SALARY_STRUCTURE" } });
		HrTableExportProjection.Value("payroll-monthly-workbench-table", "netSalary", payroll).ShouldBe(120.5m);
		HrTableExportProjection.Value("payroll-monthly-workbench-table", "status", payroll).ShouldBe("LOCKED");
		HrTableExportProjection.Value("payroll-monthly-workbench-table", "issueFilter", payroll).ShouldBe("CRITICAL");
		var attendance = JsonSerializer.SerializeToElement(new { fullName = "Synthetic Staff", status = "NotMarked", inTime = "09:05:00" });
		HrTableExportProjection.Value("hr-manual-attendance", "status", attendance).ShouldBe("Pending");
		HrTableExportProjection.Value("hr-manual-attendance", "timeIn", attendance).ShouldBe("09:05:00");
	}
}
