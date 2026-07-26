using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Reports;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HrReportApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/reports";

	public HrReportApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Preview_Should_Return_Paged_Report_Rows()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Columns = ["employeeCode", "employeeName", "department"],
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Employee);
		preview.Page.ShouldBe(1);
		preview.PageSize.ShouldBeLessThanOrEqualTo(10);
		preview.Rows.Count.ShouldBeLessThanOrEqualTo(preview.PageSize);
		preview.Columns.Select(x => x.Id).ShouldBe(["employeeCode", "employeeName", "department"]);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Preview_Should_Treat_Legacy_In_Filter_Value_As_Single_Value()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var baselineResponse = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Columns = ["employeeCode", "employeeName"],
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto()
		}, JsonOptions);
		baselineResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var baseline = await baselineResponse.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		baseline.ShouldNotBeNull();
		baseline.Rows.ShouldNotBeEmpty();
		var employeeCode = baseline.Rows[0]["employeeCode"];
		employeeCode.ShouldNotBeNullOrWhiteSpace();

		var filteredResponse = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Columns = ["employeeCode", "employeeName"],
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto
			{
				Conditions =
				[
					new HrReportFilterConditionDto
					{
						Field = "employeeCode",
						Operator = HrReportFilterOperator.In,
						Value = $" {employeeCode} "
					}
				]
			}
		}, JsonOptions);

		filteredResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var filtered = await filteredResponse.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		filtered.ShouldNotBeNull();
		filtered.Rows.ShouldNotBeEmpty();
		filtered.Rows.Select(row => row["employeeCode"]).Distinct().ShouldBe([employeeCode]);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Preview_Should_Reject_Unknown_Report_Field_References()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Invalid Employee Report",
			DataSource = HrReportDataSources.Employee,
			Columns = ["employeeCode", "notAReportField"],
			Sorts = [new HrReportSortDto { Field = "notAReportField", Direction = "asc" }],
			Filters = new HrReportFilterDto
			{
				Conditions = [
					new HrReportFilterConditionDto
					{
						Field = "notAReportField",
						Operator = HrReportFilterOperator.Contains,
						Value = "EMP"
					}
				]
			}
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Report_Request_Save_Paths_Should_Reject_Invalid_Enum_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var previewResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/preview",
			CreateInvalidEnumReportRequest(),
			JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var chartTypeResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/preview",
			CreateInvalidChartTypeReportRequest(),
			JsonOptions);
		chartTypeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var chartValueFieldResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/preview",
			CreateInvalidChartValueFieldReportRequest(),
			JsonOptions);
		chartValueFieldResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var exportResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			CreateInvalidEnumReportRequest(),
			JsonOptions);
		exportResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var templateResponse = await Client.PostAsJsonAsync($"{BaseUrl}/templates", new HrReportTemplateRequestDto
		{
			Name = "Invalid Enum Template",
			Category = "Payroll",
			DataSource = HrReportDataSources.Payroll,
			Visibility = HrReportTemplateVisibility.Private,
			Definition = CreateInvalidEnumReportRequest()
		}, JsonOptions);
		templateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var scheduleResponse = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = "Invalid Enum Schedule",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			Definition = CreateInvalidEnumReportRequest()
		}, JsonOptions);
		scheduleResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Report_Request_Save_Paths_Should_Canonicalize_Mixed_Case_Enum_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var previewResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/preview",
			CreateMixedCasePayrollReportRequest(),
			JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await previewResponse.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.Columns.Select(x => x.Id).ShouldContain("sum_grossSalary");
		preview.Chart.ShouldNotBeNull();
		preview.Chart.Type.ShouldBe(HrReportChartType.Bar);
		preview.NormalizedFilters.Conditions.ShouldNotBeNull();
		preview.NormalizedFilters.Conditions[0].Operator.ShouldBe(HrReportFilterOperator.In);

		var kpiResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/preview",
			new HrReportRequestDto
			{
				ReportType = "Mixed Case KPI Report",
				DataSource = HrReportDataSources.Employee,
				Chart = new HrReportChartDefinitionDto
				{
					Type = "KPI"
				},
				Filters = new HrReportFilterDto()
			},
			JsonOptions);
		kpiResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var kpiPreview = await kpiResponse.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		kpiPreview.ShouldNotBeNull();
		kpiPreview.Chart.ShouldNotBeNull();
		kpiPreview.Chart.Type.ShouldBe(HrReportChartType.Kpi);

		var exportResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			CreateMixedCasePayrollReportRequest(),
			JsonOptions);
		exportResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await exportResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var jobResponse = await Client.GetAsync($"{BaseUrl}/exports/{created.JobId}");
		jobResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var job = await jobResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		job.ShouldNotBeNull();
		job.RequestSnapshot.ShouldNotBeNull();
		job.RequestSnapshot.Sorts.ShouldNotBeNull();
		job.RequestSnapshot.Sorts[0].Direction.ShouldBe("desc");
		job.RequestSnapshot.Summaries.ShouldNotBeNull();
		job.RequestSnapshot.Summaries[0].Function.ShouldBe(HrReportSummaryFunction.Sum);
		job.RequestSnapshot.Chart.ShouldNotBeNull();
		job.RequestSnapshot.Chart.Type.ShouldBe(HrReportChartType.Bar);
		job.RequestSnapshot.Chart.Function.ShouldBe(HrReportSummaryFunction.Average);
		job.RequestSnapshot.Filters.Conditions.ShouldNotBeNull();
		job.RequestSnapshot.Filters.Conditions[0].Operator.ShouldBe(HrReportFilterOperator.In);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Preview_Should_Use_Source_Default_Columns_When_Columns_Are_Not_Provided()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Default Payroll Report",
			DataSource = HrReportDataSources.Payroll,
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Payroll);
		preview.Columns.Select(x => x.Id).ShouldBe([
			"month",
			"employeeCode",
			"employeeName",
			"department",
			"grossSalary",
			"totalDeduction",
			"netPay",
			"reviewStatus"
		]);
	}

	[Theory]
	[InlineData(HrReportDataSources.Employee)]
	[InlineData(HrReportDataSources.Payroll)]
	[InlineData(HrReportDataSources.Attendance)]
	[InlineData(HrReportDataSources.Leave)]
	[InlineData(HrReportDataSources.Policies)]
	[InlineData(HrReportDataSources.Performance)]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Preview_Should_Work_For_Each_Hr_Report_Source(string dataSource)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = $"{dataSource} smoke",
			DataSource = dataSource,
			Page = 1,
			PageSize = 5,
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(dataSource);
		preview.Columns.Count.ShouldBeGreaterThan(0);
		preview.Rows.Count.ShouldBeLessThanOrEqualTo(5);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Policy_Report_Should_Only_Return_Assignments_In_UserScope()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var scopedUserId = Guid.NewGuid();
		var inScopeStageId = EmployeeTestHelper.DefaultMediumStageId;
		var outOfScopeStageId = await SeedReportStageAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var inScopeEmployee = await CreateEmployeeInStageAsync($"RptIn{marker}", inScopeStageId);
		var outOfScopeEmployee = await CreateEmployeeInStageAsync($"RptOut{marker}", outOfScopeStageId);
		inScopeEmployee.UserId.ShouldNotBeNull();
		outOfScopeEmployee.UserId.ShouldNotBeNull();
		var inScopeTarget = $"In-scope policy target {marker}";
		var outOfScopeTarget = $"Out-of-scope policy target {marker}";

		await EnsureUserHasStageScopeAsync(inScopeEmployee.UserId.Value, inScopeStageId);
		await EnsureUserHasStageScopeAsync(outOfScopeEmployee.UserId.Value, outOfScopeStageId);
		await SeedEmployeePolicyAssignmentsAsync(
			(inScopeEmployee.Id, inScopeTarget),
			(outOfScopeEmployee.Id, outOfScopeTarget));
		await EnsureUserHasStageScopeAsync(scopedUserId, inScopeStageId);
		using var scopedClient = CreateReportViewClient(scopedUserId);

		var response = await scopedClient.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Scoped Policy Report",
			DataSource = HrReportDataSources.Policies,
			Columns = ["recordType", "target", "summary"],
			Page = 1,
			PageSize = 20,
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		var targets = preview.Rows
			.Select(row => row.GetValueOrDefault("target"))
			.Where(target => target?.Contains(marker, StringComparison.Ordinal) == true)
			.ToList();
		targets.ShouldContain(inScopeTarget);
		targets.ShouldNotContain(outOfScopeTarget);
	}

	[Theory]
	[InlineData(HrReportDataSources.Employee)]
	[InlineData(HrReportDataSources.Attendance)]
	[InlineData(HrReportDataSources.Leave)]
	[InlineData(HrReportDataSources.Payroll)]
	[InlineData(HrReportDataSources.Performance)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Employee_Backed_Report_Should_Only_Return_Rows_In_UserScope(string dataSource)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var inScopeStageId = EmployeeTestHelper.DefaultMediumStageId;
		var outOfScopeStageId = await SeedReportStageAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var inScopeEmployee = await CreateEmployeeInStageAsync($"ScopeIn{marker}", inScopeStageId);
		var outOfScopeEmployee = await CreateEmployeeInStageAsync($"ScopeOut{marker}", outOfScopeStageId);
		inScopeEmployee.UserId.ShouldNotBeNull();

		await SeedEmployeeBackedReportRowsAsync(dataSource, inScopeEmployee, outOfScopeEmployee, inScopeStageId, outOfScopeStageId);
		await EnsureUserHasStageScopeAsync(inScopeEmployee.UserId.Value, inScopeStageId);
		using var scopedClient = CreateReportViewClient(inScopeEmployee.UserId.Value);

		var response = await scopedClient.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = $"Scoped {dataSource} Report",
			DataSource = dataSource,
			Columns = ["employeeCode", "employeeName"],
			Page = 1,
			PageSize = 50,
			Filters = BuildScopedReportFilters(dataSource, inScopeEmployee.Id, outOfScopeEmployee.Id)
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		var employeeCodes = preview.Rows
			.Select(row => row.GetValueOrDefault("employeeCode"))
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.ToList();

		employeeCodes.ShouldContain(inScopeEmployee.EmployeeCode);
		employeeCodes.ShouldNotContain(outOfScopeEmployee.EmployeeCode);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Employee_Table_Preview_Should_Use_Database_Backed_Paging_And_Counts()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Columns = ["employeeCode", "employeeName", "department"],
			Sorts = [new HrReportSortDto { Field = "employeeCode", Direction = "desc" }],
			Page = 1,
			PageSize = 1,
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Employee);
		preview.Rows.Count.ShouldBeLessThanOrEqualTo(1);
		preview.TotalCount.ShouldBeGreaterThanOrEqualTo(preview.Rows.Count);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
	}

	[Theory]
	[InlineData(HrReportDataSources.Payroll, "employeeCode")]
	[InlineData(HrReportDataSources.Attendance, "attendanceDate")]
	[InlineData(HrReportDataSources.Leave, "startDate")]
	[InlineData(HrReportDataSources.Policies, "recordType")]
	[InlineData(HrReportDataSources.Performance, "cycle")]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Core_Table_Previews_Should_Use_Database_Backed_Paging_And_Counts(string dataSource, string sortField)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = $"{dataSource} query-backed preview",
			DataSource = dataSource,
			Sorts = [new HrReportSortDto { Field = sortField, Direction = "desc" }],
			Page = 1,
			PageSize = 1,
			Filters = dataSource == HrReportDataSources.Attendance
				? new HrReportFilterDto
				{
					DateField = "attendanceDate",
					FromDate = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
					ToDate = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month))
				}
				: new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(dataSource);
		preview.Rows.Count.ShouldBeLessThanOrEqualTo(1);
		preview.TotalCount.ShouldBeGreaterThanOrEqualTo(preview.Rows.Count);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Employee_Grouped_Preview_Should_Use_Database_Backed_Counts_And_Chart_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Employee Department Analytics",
			DataSource = HrReportDataSources.Employee,
			Page = 1,
			PageSize = 5,
			Groups = ["department"],
			Sorts = [new HrReportSortDto { Field = "rowCount", Direction = "desc" }],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Bar,
				CategoryField = "department",
				Function = HrReportSummaryFunction.Count
			},
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Employee);
		preview.Rows.Count.ShouldBeLessThanOrEqualTo(5);
		preview.TotalCount.ShouldBeGreaterThanOrEqualTo(preview.Rows.Count);
		preview.Columns.Select(x => x.Id).ShouldBe(["rowCount", "department"]);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
		preview.Chart.ShouldNotBeNull();
		preview.Chart.Points.ShouldNotBeEmpty();

		foreach (var point in preview.Chart.Points)
		{
			var matchingRow = preview.Rows.FirstOrDefault(row =>
				row.TryGetValue("department", out var department) && department == point.Label);
			if (matchingRow == null)
			{
				continue;
			}

			matchingRow.TryGetValue("rowCount", out var rawRowCount).ShouldBeTrue();
			decimal.Parse(rawRowCount ?? "0", CultureInfo.InvariantCulture).ShouldBe(point.Value);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Payroll_Grouped_Preview_Should_Use_Database_Backed_Summaries_And_Chart_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Payroll Department Analytics",
			DataSource = HrReportDataSources.Payroll,
			Page = 1,
			PageSize = 5,
			Groups = ["department"],
			Summaries = [new HrReportSummaryDto { Field = "netPay", Function = HrReportSummaryFunction.Sum }],
			Sorts = [new HrReportSortDto { Field = "sum_netPay", Direction = "desc" }],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Bar,
				CategoryField = "department",
				ValueField = "netPay",
				Function = HrReportSummaryFunction.Sum
			},
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Payroll);
		preview.Columns.Select(x => x.Id).ShouldBe(["rowCount", "department", "sum_netPay"]);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
		preview.Chart.ShouldNotBeNull();
		if (preview.Rows.Count > 0)
		{
			preview.Chart.Points.ShouldNotBeEmpty();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Attendance_Grouped_Preview_Should_Use_Database_Backed_Counts_And_Chart_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var now = DateTime.UtcNow;

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Attendance Status Analytics",
			DataSource = HrReportDataSources.Attendance,
			Page = 1,
			PageSize = 5,
			Groups = ["status"],
			Sorts = [new HrReportSortDto { Field = "rowCount", Direction = "desc" }],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Donut,
				CategoryField = "status",
				Function = HrReportSummaryFunction.Count
			},
			Filters = new HrReportFilterDto
			{
				DateField = "attendanceDate",
				FromDate = new DateOnly(now.Year, now.Month, 1),
				ToDate = new DateOnly(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month))
			}
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Attendance);
		preview.Columns.Select(x => x.Id).ShouldBe(["rowCount", "status"]);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
		preview.Chart.ShouldNotBeNull();
		preview.Chart.Type.ShouldBe(HrReportChartType.Donut);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Leave_Grouped_Preview_Should_Use_Database_Backed_Summaries_And_Chart_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Leave Status Analytics",
			DataSource = HrReportDataSources.Leave,
			Page = 1,
			PageSize = 5,
			Groups = ["status"],
			Summaries = [new HrReportSummaryDto { Field = "approvedUnits", Function = HrReportSummaryFunction.Sum }],
			Sorts = [new HrReportSortDto { Field = "sum_approvedUnits", Direction = "desc" }],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Bar,
				CategoryField = "status",
				ValueField = "approvedUnits",
				Function = HrReportSummaryFunction.Sum
			},
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Leave);
		preview.Columns.Select(x => x.Id).ShouldBe(["rowCount", "status", "sum_approvedUnits"]);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
		preview.Chart.ShouldNotBeNull();
		if (preview.Rows.Count > 0)
		{
			preview.Chart.Points.ShouldNotBeEmpty();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Performance_Grouped_Preview_Should_Use_Database_Backed_Summaries_And_Chart_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Performance Department Analytics",
			DataSource = HrReportDataSources.Performance,
			Page = 1,
			PageSize = 5,
			Groups = ["department"],
			Summaries = [new HrReportSummaryDto { Field = "finalScore", Function = HrReportSummaryFunction.Average }],
			Sorts = [new HrReportSortDto { Field = "avg_finalScore", Direction = "desc" }],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Line,
				CategoryField = "department",
				ValueField = "finalScore",
				Function = HrReportSummaryFunction.Average
			},
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Performance);
		preview.Columns.Select(x => x.Id).ShouldBe(["rowCount", "department", "avg_finalScore"]);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
		preview.Chart.ShouldNotBeNull();
		if (preview.Rows.Count > 0)
		{
			preview.Chart.Points.ShouldNotBeEmpty();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Policies_Grouped_Preview_Should_Use_Database_Backed_Counts_And_Chart_Values()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Policy Record Type Analytics",
			DataSource = HrReportDataSources.Policies,
			Page = 1,
			PageSize = 5,
			Groups = ["recordType"],
			Sorts = [new HrReportSortDto { Field = "rowCount", Direction = "desc" }],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Donut,
				CategoryField = "recordType",
				Function = HrReportSummaryFunction.Count
			},
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.DataSource.ShouldBe(HrReportDataSources.Policies);
		preview.Columns.Select(x => x.Id).ShouldBe(["rowCount", "recordType"]);
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
		preview.Chart.ShouldNotBeNull();
		if (preview.Rows.Count > 0)
		{
			preview.Chart.Points.ShouldNotBeEmpty();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Preview_Should_Filter_Employee_Report_By_Selected_Date_Field()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto
			{
				DateField = "dateOfJoining",
				FromDate = new DateOnly(2099, 1, 1),
				ToDate = new DateOnly(2099, 12, 31)
			}
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.TotalCount.ShouldBe(0);
		preview.NormalizedFilters.DateField.ShouldBe("dateOfJoining");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Preview_Should_Filter_Payroll_Report_By_Month()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.MonthlyPayrollSummary,
			DataSource = HrReportDataSources.Payroll,
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto
			{
				Month = "2099-01"
			}
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.TotalCount.ShouldBe(0);
		preview.NormalizedFilters.Month.ShouldBe("2099-01");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Preview_Should_Filter_Performance_Report_By_Finalized_Date()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Performance Future Window",
			DataSource = HrReportDataSources.Performance,
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto
			{
				DateField = "finalizedAt",
				FromDate = new DateOnly(2099, 1, 1),
				ToDate = new DateOnly(2099, 12, 31)
			}
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.TotalCount.ShouldBe(0);
		preview.NormalizedFilters.DateField.ShouldBe("finalizedAt");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Sources_Should_Return_Report_Builder_Metadata()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync($"{BaseUrl}/sources");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var sources = await response.Content.ReadFromJsonAsync<HrReportSourcesDto>(JsonOptions);
		sources.ShouldNotBeNull();
		sources.Sources.Select(x => x.Id).ShouldContain(HrReportDataSources.Employee);
		sources.Sources.Select(x => x.Id).ShouldContain(HrReportDataSources.Payroll);
		sources.Sources.Select(x => x.Id).ShouldContain(HrReportDataSources.Attendance);
		sources.Sources.Select(x => x.Id).ShouldContain(HrReportDataSources.Leave);
		sources.Sources.Select(x => x.Id).ShouldContain(HrReportDataSources.Policies);
		sources.Sources.Select(x => x.Id).ShouldContain(HrReportDataSources.Performance);
		sources.Operators.ShouldContain(x => x.Id == HrReportFilterOperator.Contains);
		sources.Sources.ShouldAllBe(source => source.DefaultColumns.Count > 0);
		sources.Sources.SelectMany(x => x.Fields).ShouldAllBe(field => field.Filterable);
		sources.Sources.SelectMany(x => x.Fields).ShouldAllBe(field => field.Sortable);
		sources.Sources.ShouldAllBe(source => source.Fields.Any(field => field.Chartable));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Preview_Should_Reject_Unsupported_Source_Capabilities()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Unsupported Employee Advanced Preview",
			DataSource = HrReportDataSources.Employee,
			Page = 1,
			PageSize = 5,
			Groups = ["email"],
			Summaries = [new HrReportSummaryDto { Field = "email", Function = HrReportSummaryFunction.Sum }],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Bar,
				CategoryField = "email",
				ValueField = "email",
				Function = HrReportSummaryFunction.Sum
			},
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Templates_Should_Create_List_Duplicate_And_Delete()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/templates", new HrReportTemplateRequestDto
		{
			Name = "Employee Directory",
			Category = "Employee",
			DataSource = HrReportDataSources.Employee,
			Visibility = HrReportTemplateVisibility.Private,
			Definition = new HrReportRequestDto
			{
				ReportType = "Employee Directory",
				DataSource = HrReportDataSources.Employee,
				Columns = ["employeeCode", "employeeName", "email"]
			}
		}, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportTemplateDto>(JsonOptions);
		created.ShouldNotBeNull();
		created.Id.ShouldNotBe(default);

		var listResponse = await Client.GetAsync($"{BaseUrl}/templates");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var templates = await listResponse.Content.ReadFromJsonAsync<HrReportTemplateListDto>(JsonOptions);
		templates.ShouldNotBeNull();
		templates.Items.ShouldContain(x => x.Id == created.Id);

		var duplicateResponse = await Client.PostAsync($"{BaseUrl}/templates/{created.Id}/duplicate", null);
		duplicateResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<HrReportTemplateDto>(JsonOptions);
		duplicate.ShouldNotBeNull();
		duplicate.Name.ShouldContain("Copy");

		var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/templates/{created.Id}");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Export_Should_Create_Queued_Job_And_Return_Job_Status()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var request = new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			TemplateId = Guid.NewGuid(),
			TemplateName = "Employee Template",
			SourceAction = "scheduleRun",
			ParentJobId = Guid.NewGuid(),
			Filters = new HrReportFilterDto()
		};

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", request, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var createResult = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		createResult.ShouldNotBeNull();
		createResult.JobId.ShouldNotBe(default);

		var jobResponse = await Client.GetAsync($"{BaseUrl}/exports/{createResult.JobId}");
		jobResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var job = await jobResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		job.ShouldNotBeNull();
		job.Id.ShouldBe(createResult.JobId);
		job.RequestSnapshot.ShouldNotBeNull();
		job.RequestSnapshot.DataSource.ShouldBe(HrReportDataSources.Employee);
		job.TemplateName.ShouldBe("Employee Template");
		job.SourceAction.ShouldBe("manualExport");
		job.ParentJobId.ShouldBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Export_Details_Should_Tolerate_Malformed_Legacy_Request_Snapshot()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var jobId = Guid.NewGuid();

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
			var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

			using (currentTenant.Change(tenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				await exportRepository.InsertAsync(
					new HrReportExportJob(
						jobId,
						tenantId,
						Guid.Parse(TestAuthHandler.TestUserId),
						"Legacy Corrupt Snapshot",
						HrReportFormat.Excel,
						"{}",
						"{ malformed-json",
						DateTime.UtcNow.AddDays(7)),
					autoSave: false);
				await uow.CompleteAsync();
			}
		}

		var response = await Client.GetAsync($"{BaseUrl}/exports/{jobId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var job = await response.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		job.ShouldNotBeNull();
		job.RequestSnapshot.ShouldNotBeNull();
		job.RequestSnapshot.ReportType.ShouldBe("Legacy Corrupt Snapshot");
		job.RequestSnapshot.Format.ShouldBe(HrReportFormat.Excel);
		job.SourceAction.ShouldBe("manualExport");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Export_Job_Should_Generate_Excel_And_Download()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var request = new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Columns = ["employeeCode", "employeeName", "department"],
			Filters = new HrReportFilterDto()
		};

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", request, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var createResult = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		createResult.ShouldNotBeNull();
		var generationId = await GetExportGenerationIdAsync(createResult.JobId);

		using (var scope = Factory.Services.CreateScope())
		{
			var job = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await job.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = createResult.JobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = Guid.Parse(TestAuthHandler.TestUserId),
				Request = request,
				GenerationId = generationId
			});
		}

		var jobResponse = await Client.GetAsync($"{BaseUrl}/exports/{createResult.JobId}");
		jobResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var completedJob = await jobResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		completedJob.ShouldNotBeNull();
		completedJob.Status.ShouldBe(HrReportExportStatus.Completed);

		var downloadResponse = await Client.GetAsync($"{BaseUrl}/exports/{createResult.JobId}/download");
		downloadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		downloadResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		var bytes = await downloadResponse.Content.ReadAsByteArrayAsync();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Export_Worker_Should_Ignore_Stale_Queued_Scope_Arguments()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.NewGuid();
		var inScopeStageId = EmployeeTestHelper.DefaultMediumStageId;
		var outOfScopeStageId = await SeedReportStageAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var inScopeEmployee = await CreateEmployeeInStageAsync($"WorkerIn{marker}", inScopeStageId);
		var outOfScopeEmployee = await CreateEmployeeInStageAsync($"WorkerOut{marker}", outOfScopeStageId);
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-worker-owner-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await EnsureUserHasStageScopeAsync(ownerUserId, inScopeStageId);

		using var ownerClient = CreateReportClient(
			ownerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Export);
		var request = new HrReportRequestDto
		{
			ReportType = $"Persisted Worker Scope {marker}",
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Columns = ["employeeCode", "employeeName"],
			Filters = new HrReportFilterDto
			{
				EmployeeIds = [inScopeEmployee.Id, outOfScopeEmployee.Id]
			}
		};
		var createResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			request,
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();
		var generationId = await GetExportGenerationIdAsync(created.JobId);

		using (var scope = Factory.Services.CreateScope())
		{
			var worker = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await worker.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = created.JobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = ownerUserId,
				Request = request,
				GenerationId = generationId,
				ScopeSnapshot = new HrReportExportScopeSnapshot
				{
					TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
					HasTenantWideAccess = true
				}
			});
		}

		var detailsResponse = await ownerClient.GetAsync($"{BaseUrl}/exports/{created.JobId}");
		detailsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var completed = await detailsResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		completed.ShouldNotBeNull();
		completed.Status.ShouldBe(HrReportExportStatus.Completed);
		completed.RowCount.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Retry_Should_Fence_Stale_Queued_Generation()
	{
		var ownerUserId = Guid.NewGuid();
		var replacementStageId = await SeedReportStageAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-retry-fence-owner-{marker}",
			grantReportView: true);
		using var ownerClient = CreateReportClient(
			ownerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Export);
		var request = CreateBasicEmployeeExportRequest($"Retry Generation Fence {marker}");
		var createResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			request,
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();
		var originalGenerationId = await GetExportGenerationIdAsync(created.JobId);

		var cancelResponse = await ownerClient.PostAsync(
			$"{BaseUrl}/exports/{created.JobId}/cancel",
			null);
		cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		await ReplaceUserStageScopeAsync(ownerUserId, replacementStageId);
		var retryResponse = await ownerClient.PostAsync(
			$"{BaseUrl}/exports/{created.JobId}/retry",
			null);
		retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var retriedGenerationId = await GetExportGenerationIdAsync(created.JobId);
		retriedGenerationId.ShouldNotBe(originalGenerationId);
		var retriedScope = await GetExportScopeSnapshotAsync(created.JobId);
		retriedScope.HasTenantWideAccess.ShouldBeFalse();
		retriedScope.MediumStageIds.ShouldBe([replacementStageId]);

		using (var scope = Factory.Services.CreateScope())
		{
			var worker = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await worker.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = created.JobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = ownerUserId,
				Request = request,
				GenerationId = originalGenerationId,
				ScopeSnapshot = new HrReportExportScopeSnapshot
				{
					TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
					HasTenantWideAccess = true
				}
			});
		}

		var detailsResponse = await ownerClient.GetAsync($"{BaseUrl}/exports/{created.JobId}");
		detailsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var retried = await detailsResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		retried.ShouldNotBeNull();
		retried.Status.ShouldBe(HrReportExportStatus.Queued);
		(await GetExportGenerationIdAsync(created.JobId)).ShouldBe(retriedGenerationId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Export_Worker_Should_Persist_Current_Scope_Before_Generating_Legacy_Job()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.NewGuid();
		var inScopeStageId = EmployeeTestHelper.DefaultMediumStageId;
		var outOfScopeStageId = await SeedReportStageAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var inScopeEmployee = await CreateEmployeeInStageAsync($"LegacyIn{marker}", inScopeStageId);
		var outOfScopeEmployee = await CreateEmployeeInStageAsync($"LegacyOut{marker}", outOfScopeStageId);
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-legacy-worker-owner-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await EnsureUserHasStageScopeAsync(ownerUserId, inScopeStageId);

		var request = new HrReportRequestDto
		{
			ReportType = $"Legacy Worker Scope {marker}",
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Columns = ["employeeCode", "employeeName"],
			Filters = new HrReportFilterDto
			{
				EmployeeIds = [inScopeEmployee.Id, outOfScopeEmployee.Id]
			}
		};
		var jobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Queued,
			marker,
			request: request);

		using (var scope = Factory.Services.CreateScope())
		{
			var worker = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await worker.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = jobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = ownerUserId,
				Request = request
			});
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			{
				var completed = await exportRepository.GetAsync(jobId);
				completed.Status.ShouldBe(HrReportExportStatus.Completed);
				completed.RowCount.ShouldBe(1);
				using var storedRequest = JsonDocument.Parse(completed.RequestJson);
				var persistedScope = storedRequest.RootElement.GetProperty("scope");
				persistedScope.GetProperty("hasTenantWideAccess").GetBoolean().ShouldBeFalse();
				var persistedStageIds = persistedScope
					.GetProperty("mediumStageIds")
					.EnumerateArray()
					.Select(x => x.GetGuid())
					.ToList();
				persistedStageIds.ShouldContain(inScopeStageId);
				persistedStageIds.ShouldNotContain(outOfScopeStageId);
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Export_Worker_Should_Fail_Legacy_Job_When_Owner_Has_No_Effective_Scope()
	{
		var ownerUserId = Guid.NewGuid();
		var marker = Guid.NewGuid().ToString("N")[..8];
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-legacy-no-scope-owner-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		var request = CreateBasicEmployeeExportRequest($"Legacy Missing Scope {marker}");
		var jobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Queued,
			marker,
			request: request);

		using (var scope = Factory.Services.CreateScope())
		{
			var worker = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await worker.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = jobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = ownerUserId,
				Request = request
			});
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			{
				var failed = await exportRepository.GetAsync(jobId);
				failed.Status.ShouldBe(HrReportExportStatus.Failed);
				failed.BlobName.ShouldBeNull();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Export_Owner_Should_Lose_Read_And_Share_Access_When_Scope_Narrows()
	{
		var ownerUserId = Guid.NewGuid();
		var recipientUserId = Guid.NewGuid();
		var remainingStageId = await SeedReportStageAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-revoked-owner-{marker}",
			grantReportView: true);
		await SeedReportRecipientLookupUserAsync(
			recipientUserId,
			$"report-revoked-recipient-{marker}",
			grantReportView: true);

		using var ownerClient = CreateReportClient(
			ownerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Export,
			ShikshaMitraSubmodulePermissions.HR.Reports.Share);
		var request = CreateBasicEmployeeExportRequest($"Owner Scope Revocation {marker}");
		var createResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			request,
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();
		var generationId = await GetExportGenerationIdAsync(created.JobId);
		using (var scope = Factory.Services.CreateScope())
		{
			var worker = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await worker.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = created.JobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = ownerUserId,
				Request = request,
				GenerationId = generationId
			});
		}

		await ReplaceUserStageScopeAsync(ownerUserId, remainingStageId);

		var detailsResponse = await ownerClient.GetAsync($"{BaseUrl}/exports/{created.JobId}");
		detailsResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		var listResponse = await ownerClient.GetAsync($"{BaseUrl}/exports");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var exports = await listResponse.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		exports.ShouldNotBeNull();
		exports.Select(x => x.Id).ShouldNotContain(created.JobId);
		var downloadResponse = await ownerClient.GetAsync($"{BaseUrl}/exports/{created.JobId}/download");
		downloadResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		var shareResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports/{created.JobId}/share",
			new HrReportShareExportRequestDto { UserIds = [recipientUserId] },
			JsonOptions);
		shareResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Shared_Completed_Export_Should_Download_For_View_Recipient()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var recipientUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(recipientUserId, $"report-download-recipient-{marker}", grantReportView: true);

		var request = new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Columns = ["employeeCode", "employeeName"],
			Filters = new HrReportFilterDto()
		};
		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", request, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var createResult = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		createResult.ShouldNotBeNull();
		var generationId = await GetExportGenerationIdAsync(createResult.JobId);

		using (var scope = Factory.Services.CreateScope())
		{
			var job = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await job.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = createResult.JobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = Guid.Parse(TestAuthHandler.TestUserId),
				Request = request,
				GenerationId = generationId
			});
		}

		var shareResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports/{createResult.JobId}/share", new HrReportShareExportRequestDto
		{
			UserIds = [recipientUserId]
		}, JsonOptions);
		shareResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var recipientClient = CreateReportViewClient(recipientUserId);
		var recipientExportsResponse = await recipientClient.GetAsync($"{BaseUrl}/exports");
		recipientExportsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var recipientExports = await recipientExportsResponse.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		recipientExports.ShouldNotBeNull();
		recipientExports.Select(x => x.Id).ShouldContain(createResult.JobId);

		var downloadResponse = await recipientClient.GetAsync($"{BaseUrl}/exports/{createResult.JobId}/download");
		downloadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var bytes = await downloadResponse.Content.ReadAsByteArrayAsync();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Preview_Should_Return_Chart_And_Query_Warnings()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = "Employee Department Analytics",
			DataSource = HrReportDataSources.Employee,
			Page = 1,
			PageSize = 10,
			Groups = ["department"],
			Chart = new HrReportChartDefinitionDto
			{
				Type = HrReportChartType.Bar,
				CategoryField = "department",
				Function = HrReportSummaryFunction.Count
			},
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.Chart.ShouldNotBeNull();
		preview.Chart.Type.ShouldBe(HrReportChartType.Bar);
		preview.Chart.Points.ShouldNotBeEmpty();
		preview.QueryWarnings.ShouldNotContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Export_Should_Cancel_And_Retry()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Filters = new HrReportFilterDto()
		}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var cancelResponse = await Client.PostAsync($"{BaseUrl}/exports/{created.JobId}/cancel", null);
		cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var cancelled = await cancelResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		cancelled.ShouldNotBeNull();
		cancelled.Status.ShouldBe(HrReportExportStatus.Cancelled);
		cancelled.ProgressPercent.ShouldBe(100);

		var retryResponse = await Client.PostAsync($"{BaseUrl}/exports/{created.JobId}/retry", null);
		retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var retry = await retryResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		retry.ShouldNotBeNull();
		retry.JobId.ShouldBe(created.JobId);
		retry.Status.ShouldBe(HrReportExportStatus.Queued);

		var retriedJobResponse = await Client.GetAsync($"{BaseUrl}/exports/{created.JobId}");
		retriedJobResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var retriedJob = await retriedJobResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		retriedJob.ShouldNotBeNull();
		retriedJob.SourceAction.ShouldBe("retry");
		retriedJob.ParentJobId.ShouldBe(created.JobId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Export_Actions_Should_Reject_Invalid_Status_Transitions()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var marker = Guid.NewGuid().ToString("N")[..8];

		foreach (var status in new[] { HrReportExportStatus.Completed, HrReportExportStatus.Queued, HrReportExportStatus.Running })
		{
			var jobId = await SeedExportJobAsync(ownerUserId, status, $"retry-invalid-{status}-{marker}");
			var response = await Client.PostAsync($"{BaseUrl}/exports/{jobId}/retry", null);
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			await AssertProblemContainsAsync(response, "status", status);
		}

		foreach (var status in new[] { HrReportExportStatus.Failed, HrReportExportStatus.Cancelled })
		{
			var jobId = await SeedExportJobAsync(ownerUserId, status, $"retry-valid-{status}-{marker}");
			var response = await Client.PostAsync($"{BaseUrl}/exports/{jobId}/retry", null);
			response.StatusCode.ShouldBe(HttpStatusCode.OK);
			var result = await response.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
			result.ShouldNotBeNull();
			result.JobId.ShouldBe(jobId);
			result.Status.ShouldBe(HrReportExportStatus.Queued);
		}

		foreach (var status in new[] { HrReportExportStatus.Failed, HrReportExportStatus.Cancelled, HrReportExportStatus.Queued, HrReportExportStatus.Running })
		{
			var jobId = await SeedExportJobAsync(ownerUserId, status, $"regenerate-invalid-{status}-{marker}");
			var response = await Client.PostAsync($"{BaseUrl}/exports/{jobId}/regenerate", null);
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			await AssertProblemContainsAsync(response, "status", status);
		}

		var completedJobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Completed,
			$"regenerate-not-expired-{marker}",
			DateTime.UtcNow.AddDays(7));
		var nonExpiredResponse = await Client.PostAsync($"{BaseUrl}/exports/{completedJobId}/regenerate", null);
		nonExpiredResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		await AssertProblemContainsAsync(nonExpiredResponse, "status", HrReportExportStatus.Completed);

		var expiredJobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Completed,
			$"regenerate-expired-{marker}",
			DateTime.UtcNow.AddDays(-1));
		var regenerateResponse = await Client.PostAsync($"{BaseUrl}/exports/{expiredJobId}/regenerate", null);
		regenerateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var regenerateResult = await regenerateResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		regenerateResult.ShouldNotBeNull();
		regenerateResult.JobId.ShouldNotBe(expiredJobId);
		regenerateResult.Status.ShouldBe(HrReportExportStatus.Queued);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Export_List_Should_Filter_By_Requested_Date_Before_Limiting_Results()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var marker = Guid.NewGuid().ToString("N")[..8];
		var from = DateTime.UtcNow.Date.AddDays(-3);
		var to = DateTime.UtcNow.Date.AddDays(1);
		var includedJobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Failed,
			$"requested-date-included-{marker}",
			creationTime: from.AddHours(1));
		var excludedJobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Failed,
			$"requested-date-excluded-{marker}",
			creationTime: from.AddDays(-1));

		var response = await Client.GetAsync(
			$"{BaseUrl}/exports?requestedFrom={Uri.EscapeDataString(from.ToString("O"))}&requestedTo={Uri.EscapeDataString(to.ToString("O"))}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var exports = await response.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		exports.ShouldNotBeNull();
		exports.Select(x => x.Id).ShouldContain(includedJobId);
		exports.Select(x => x.Id).ShouldNotContain(excludedJobId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Failed_Export_Delete_Should_Be_Owner_Scoped_And_Soft_Delete_The_Job()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var otherUserId = Guid.NewGuid();
		var marker = Guid.NewGuid().ToString("N")[..8];
		await SeedReportRecipientLookupUserAsync(
			otherUserId,
			$"report-export-delete-{marker}",
			grantReportView: true);
		var failedJobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Failed,
			$"delete-failed-{marker}");

		using var otherClient = CreateReportClient(
			otherUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Delete);
		var otherDeleteResponse = await otherClient.DeleteAsync($"{BaseUrl}/exports/{failedJobId}");
		otherDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var ownerDeleteResponse = await Client.DeleteAsync($"{BaseUrl}/exports/{failedJobId}");
		ownerDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var detailsResponse = await Client.GetAsync($"{BaseUrl}/exports/{failedJobId}");
		detailsResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Failed_Export_Delete_Should_Reject_Non_Failed_Statuses()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var queuedJobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Queued,
			$"delete-queued-{Guid.NewGuid():N}");

		var response = await Client.DeleteAsync($"{BaseUrl}/exports/{queuedJobId}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		await AssertProblemContainsAsync(response, "status", HrReportExportStatus.Queued);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Cancelled_Running_Export_Should_Not_Be_Marked_Completed_By_Worker()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var request = CreateBasicEmployeeExportRequest("Cancelled Running Export");
		var jobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Running,
			Guid.NewGuid().ToString("N")[..8],
			request: request);

		var cancelResponse = await Client.PostAsync($"{BaseUrl}/exports/{jobId}/cancel", null);
		cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		using (var scope = Factory.Services.CreateScope())
		{
			var job = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await job.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = jobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = ownerUserId,
				Request = request
			});
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			{
				var job = await exportRepository.GetAsync(jobId);
				job.Status.ShouldBe(HrReportExportStatus.Cancelled);
				job.BlobName.ShouldBeNull();
				job.RowCount.ShouldBeNull();
				job.FileSizeBytes.ShouldBeNull();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Export_Worker_Should_Mark_Failed_When_Generation_Fails()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var ownerUserId = Guid.Parse(TestAuthHandler.TestUserId);
		var request = new HrReportRequestDto
		{
			ReportType = "Invalid Worker Export",
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Columns = ["notAReportField"],
			Filters = new HrReportFilterDto()
		};
		var jobId = await SeedExportJobAsync(
			ownerUserId,
			HrReportExportStatus.Queued,
			Guid.NewGuid().ToString("N")[..8],
			request: request);

		using (var scope = Factory.Services.CreateScope())
		{
			var job = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await job.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = jobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = ownerUserId,
				Request = request
			});
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			{
				var job = await exportRepository.GetAsync(jobId);
				job.Status.ShouldBe(HrReportExportStatus.Failed);
				job.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
				job.BlobName.ShouldBeNull();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Schedules_Should_Create_Pause_Resume_Run_And_Delete()
	{
		await EnsureDefaultScheduleOwnerCanRunReportsAsync();

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = "Monthly Employee Directory",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InAppAndEmail,
			Definition = new HrReportRequestDto
			{
				ReportType = HrReportTypes.EmployeeList,
				DataSource = HrReportDataSources.Employee,
				Columns = ["employeeCode", "employeeName"],
				SourceAction = "regenerate",
				ParentJobId = Guid.NewGuid(),
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await createResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();
		schedule.Status.ShouldBe(HrReportScheduleStatus.Active);
		schedule.RecipientUserIds.ShouldBeEmpty();

		var pauseResponse = await Client.PostAsync($"{BaseUrl}/schedules/{schedule.Id}/pause", null);
		pauseResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var paused = await pauseResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		paused.ShouldNotBeNull();
		paused.Status.ShouldBe(HrReportScheduleStatus.Paused);

		var resumeResponse = await Client.PostAsync($"{BaseUrl}/schedules/{schedule.Id}/resume", null);
		resumeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var resumed = await resumeResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		resumed.ShouldNotBeNull();
		resumed.Status.ShouldBe(HrReportScheduleStatus.Active);
		var nextRunBeforeManualRun = resumed.NextRunAt;

		var runResponse = await Client.PostAsync($"{BaseUrl}/schedules/{schedule.Id}/run-now", null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var run = await runResponse.Content.ReadFromJsonAsync<HrReportScheduleRunResponseDto>(JsonOptions);
		run.ShouldNotBeNull();
		run.ScheduleId.ShouldBe(schedule.Id);
		run.JobId.ShouldNotBe(default);
		run.NextRunAt.ShouldBe(nextRunBeforeManualRun);

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var scheduleRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportSchedule, Guid>>();
			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			{
				var persistedSchedule = await scheduleRepository.GetAsync(schedule.Id);
				persistedSchedule.GetRecipientUserIds().ShouldContain(Guid.Parse(TestAuthHandler.TestUserId));
				persistedSchedule.LastRunAt.ShouldNotBeNull();
				persistedSchedule.NextRunAt.ShouldBe(nextRunBeforeManualRun);
			}
		}

		var scheduledExportResponse = await Client.GetAsync($"{BaseUrl}/exports/{run.JobId}");
		scheduledExportResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var scheduledExport = await scheduledExportResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		scheduledExport.ShouldNotBeNull();
		scheduledExport.SourceAction.ShouldBe("scheduleRun");
		scheduledExport.ParentJobId.ShouldBeNull();

		var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/schedules/{schedule.Id}");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Due_Schedules_Should_Queue_Exports_And_Advance_Next_Run()
	{
		await EnsureDefaultScheduleOwnerCanRunReportsAsync();

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = "Due Employee Directory",
			Frequency = HrReportScheduleFrequency.Daily,
			DeliveryMode = HrReportDeliveryMode.InApp,
			FirstRunAt = DateTime.UtcNow.AddMinutes(-10),
			Definition = new HrReportRequestDto
			{
				ReportType = HrReportTypes.EmployeeList,
				DataSource = HrReportDataSources.Employee,
				Columns = ["employeeCode", "employeeName"],
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await createResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();
		var originalNextRunAt = schedule.NextRunAt;

		using (var scope = Factory.Services.CreateScope())
		{
			var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var scheduleService = scope.ServiceProvider.GetRequiredService<HrReportScheduleService>();
			using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			var processedCount = await scheduleService.ProcessDueSchedulesAsync(DateTime.UtcNow);
			processedCount.ShouldBeGreaterThanOrEqualTo(1);
			await uow.CompleteAsync();
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var multiTenantFilter = scope.ServiceProvider.GetRequiredService<Volo.Abp.Data.IDataFilter<Volo.Abp.MultiTenancy.IMultiTenant>>();
			var scheduleRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportSchedule, Guid>>();
			var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
			using (multiTenantFilter.Disable())
			{
				var persistedSchedule = await scheduleRepository.GetAsync(schedule.Id);
				persistedSchedule.LastRunAt.ShouldNotBeNull();
				persistedSchedule.NextRunAt.ShouldBeGreaterThan(originalNextRunAt);

				var exports = await exportRepository.GetListAsync(x =>
					x.RequestedByUserId == schedule.OwnerUserId &&
					x.ReportType == HrReportTypes.EmployeeList);
				exports.Any(x => x.Status == HrReportExportStatus.Queued).ShouldBeTrue();
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Due_Schedules_Should_Pause_Invalid_Owner_And_Continue_With_Later_Schedule()
	{
		var marker = Guid.NewGuid().ToString("N")[..8];
		var invalidOwnerUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(
			invalidOwnerUserId,
			$"report-schedule-invalid-owner-{marker}",
			grantReportView: true);
		using var invalidOwnerClient = CreateReportClient(
			invalidOwnerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Schedule);
		var invalidResponse = await invalidOwnerClient.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = $"Invalid Owner Due Schedule {marker}",
			Frequency = HrReportScheduleFrequency.Daily,
			DeliveryMode = HrReportDeliveryMode.InApp,
			FirstRunAt = DateTime.UtcNow.AddHours(-2),
			Definition = CreateBasicEmployeeExportRequest("Invalid Owner Due Schedule")
		}, JsonOptions);
		invalidResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var invalidSchedule = await invalidResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		invalidSchedule.ShouldNotBeNull();
		await RevokeDirectReportViewPermissionAsync(invalidOwnerUserId);

		await EnsureDefaultScheduleOwnerCanRunReportsAsync();
		var validResponse = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = $"Valid Later Due Schedule {marker}",
			Frequency = HrReportScheduleFrequency.Daily,
			DeliveryMode = HrReportDeliveryMode.InApp,
			FirstRunAt = DateTime.UtcNow.AddHours(-1),
			Definition = CreateBasicEmployeeExportRequest("Valid Later Due Schedule")
		}, JsonOptions);
		validResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var validSchedule = await validResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		validSchedule.ShouldNotBeNull();

		var dueAt = DateTime.UtcNow;
		using (var scope = Factory.Services.CreateScope())
		{
			var scheduleService = scope.ServiceProvider.GetRequiredService<HrReportScheduleService>();
			var processedCount = await scheduleService.ProcessDueSchedulesAsync(dueAt);
			processedCount.ShouldBeGreaterThanOrEqualTo(1);
		}

		DateTime validLastRunAt;
		DateTime validNextRunAt;
		using (var scope = Factory.Services.CreateScope())
		{
			var multiTenantFilter = scope.ServiceProvider.GetRequiredService<Volo.Abp.Data.IDataFilter<Volo.Abp.MultiTenancy.IMultiTenant>>();
			var scheduleRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportSchedule, Guid>>();
			using (multiTenantFilter.Disable())
			{
				var persistedInvalidSchedule = await scheduleRepository.GetAsync(invalidSchedule.Id);
				persistedInvalidSchedule.Status.ShouldBe(HrReportScheduleStatus.Paused);
				persistedInvalidSchedule.LastRunAt.ShouldBeNull();

				var persistedValidSchedule = await scheduleRepository.GetAsync(validSchedule.Id);
				persistedValidSchedule.Status.ShouldBe(HrReportScheduleStatus.Active);
				persistedValidSchedule.LastRunAt.ShouldNotBeNull();
				persistedValidSchedule.NextRunAt.ShouldBeGreaterThan(dueAt);
				validLastRunAt = persistedValidSchedule.LastRunAt.Value;
				validNextRunAt = persistedValidSchedule.NextRunAt;
			}
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var scheduleService = scope.ServiceProvider.GetRequiredService<HrReportScheduleService>();
			await scheduleService.ProcessDueSchedulesAsync(dueAt);
		}

		using (var scope = Factory.Services.CreateScope())
		{
			var multiTenantFilter = scope.ServiceProvider.GetRequiredService<Volo.Abp.Data.IDataFilter<Volo.Abp.MultiTenancy.IMultiTenant>>();
			var scheduleRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportSchedule, Guid>>();
			using (multiTenantFilter.Disable())
			{
				var persistedInvalidSchedule = await scheduleRepository.GetAsync(invalidSchedule.Id);
				persistedInvalidSchedule.Status.ShouldBe(HrReportScheduleStatus.Paused);

				var persistedValidSchedule = await scheduleRepository.GetAsync(validSchedule.Id);
				persistedValidSchedule.LastRunAt.ShouldBe(validLastRunAt);
				persistedValidSchedule.NextRunAt.ShouldBe(validNextRunAt);
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Shared_Scheduled_Exports_Should_Appear_In_Recipient_Exports_List()
	{
		await EnsureDefaultScheduleOwnerCanRunReportsAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var recipientUserId = Guid.NewGuid();
		var unrelatedUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(recipientUserId, $"report-export-recipient-{marker}", grantReportView: true);
		await SeedReportRecipientLookupUserAsync(unrelatedUserId, $"report-export-unrelated-{marker}", grantReportView: true);
		await SeedOwnedExportJobsAsync(recipientUserId, 55, marker);

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = $"Shared Employee Directory {marker}",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			RecipientUserIds = [recipientUserId],
			Definition = new HrReportRequestDto
			{
				ReportType = HrReportTypes.EmployeeList,
				DataSource = HrReportDataSources.Employee,
				Columns = ["employeeCode", "employeeName"],
				SourceAction = "regenerate",
				ParentJobId = Guid.NewGuid(),
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await createResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();
		schedule.RecipientUserIds.ShouldBe([recipientUserId]);

		var runResponse = await Client.PostAsync($"{BaseUrl}/schedules/{schedule.Id}/run-now", null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var run = await runResponse.Content.ReadFromJsonAsync<HrReportScheduleRunResponseDto>(JsonOptions);
		run.ShouldNotBeNull();

		await SeedUnrelatedExportJobsAsync(205, marker);

		using var recipientClient = CreateReportViewClient(recipientUserId);
		var recipientExportsResponse = await recipientClient.GetAsync($"{BaseUrl}/exports");
		recipientExportsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var recipientExports = await recipientExportsResponse.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		recipientExports.ShouldNotBeNull();
		recipientExports.Select(x => x.Id).ShouldContain(run.JobId);
		recipientExports.Count.ShouldBeLessThanOrEqualTo(50);

		using var unrelatedClient = CreateReportViewClient(unrelatedUserId);
		var unrelatedExportsResponse = await unrelatedClient.GetAsync($"{BaseUrl}/exports");
		unrelatedExportsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var unrelatedExports = await unrelatedExportsResponse.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		unrelatedExports.ShouldNotBeNull();
		unrelatedExports.Select(x => x.Id).ShouldNotContain(run.JobId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Shared_Export_Fallback_Should_Exact_Filter_Before_Taking_Recent_Jobs()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var recipientUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(recipientUserId, $"report-export-fallback-recipient-{marker}", grantReportView: true);
		var sharedJobId = await SeedSharedCompletedExportJobAsync(
			Guid.Parse(TestAuthHandler.TestUserId),
			recipientUserId,
			marker,
			DateTime.UtcNow.AddDays(7),
			DateTime.UtcNow.AddHours(-2));

		await SeedFalsePositiveSharedCandidateExportJobsAsync(recipientUserId, 505, marker);

		using var recipientClient = CreateReportViewClient(recipientUserId);
		var response = await recipientClient.GetAsync($"{BaseUrl}/exports");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var exports = await response.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		exports.ShouldNotBeNull();
		exports.Select(x => x.Id).ShouldContain(sharedJobId);
		exports.Count.ShouldBeLessThanOrEqualTo(50);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Shared_Export_Recipients_Should_Not_Mutate_Owner_Jobs()
	{
		await EnsureDefaultScheduleOwnerCanRunReportsAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var recipientUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(recipientUserId, $"report-export-mutation-recipient-{marker}", grantReportView: true);

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = $"Shared Mutation Guard {marker}",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			RecipientUserIds = [recipientUserId],
			Definition = new HrReportRequestDto
			{
				ReportType = HrReportTypes.EmployeeList,
				DataSource = HrReportDataSources.Employee,
				Columns = ["employeeCode", "employeeName"],
				SourceAction = "regenerate",
				ParentJobId = Guid.NewGuid(),
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await createResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();

		var runResponse = await Client.PostAsync($"{BaseUrl}/schedules/{schedule.Id}/run-now", null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var run = await runResponse.Content.ReadFromJsonAsync<HrReportScheduleRunResponseDto>(JsonOptions);
		run.ShouldNotBeNull();

		using var recipientClient = CreateReportClient(
			recipientUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Export,
			ShikshaMitraSubmodulePermissions.HR.Reports.Share);

		var recipientExportsResponse = await recipientClient.GetAsync($"{BaseUrl}/exports");
		recipientExportsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var recipientExports = await recipientExportsResponse.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		recipientExports.ShouldNotBeNull();
		recipientExports.Select(x => x.Id).ShouldContain(run.JobId);

		var cancelResponse = await recipientClient.PostAsync($"{BaseUrl}/exports/{run.JobId}/cancel", null);
		cancelResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var retryResponse = await recipientClient.PostAsync($"{BaseUrl}/exports/{run.JobId}/retry", null);
		retryResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var shareResponse = await recipientClient.PostAsJsonAsync($"{BaseUrl}/exports/{run.JobId}/share", new HrReportShareExportRequestDto
		{
			UserIds = [Guid.NewGuid()]
		}, JsonOptions);
		shareResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var expiredSharedJobId = await SeedSharedCompletedExportJobAsync(
			Guid.Parse(TestAuthHandler.TestUserId),
			recipientUserId,
			marker,
			DateTime.UtcNow.AddDays(-1));
		var regenerateResponse = await recipientClient.PostAsync($"{BaseUrl}/exports/{expiredSharedJobId}/regenerate", null);
		regenerateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var regenerated = await regenerateResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		regenerated.ShouldNotBeNull();
		regenerated.JobId.ShouldNotBe(expiredSharedJobId);

		var regeneratedJobResponse = await recipientClient.GetAsync($"{BaseUrl}/exports/{regenerated.JobId}");
		regeneratedJobResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var regeneratedJob = await regeneratedJobResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		regeneratedJob.ShouldNotBeNull();
		regeneratedJob.RequestedByUserId.ShouldBe(recipientUserId);
		regeneratedJob.ParentJobId.ShouldBe(expiredSharedJobId);
		regeneratedJob.SourceAction.ShouldBe("regenerate");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Share_Export_Should_Reject_Broad_Owner_To_Narrow_Recipient_Scope()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var recipientUserId = Guid.NewGuid();
		var recipientStageId = await SeedReportStageAsync();
		await SeedReportRecipientLookupUserAsync(
			recipientUserId,
			$"report-scope-narrow-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await EnsureUserHasStageScopeAsync(recipientUserId, recipientStageId);

		var createResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			CreateBasicEmployeeExportRequest("Broad Owner Scope Guard"),
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var shareResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/exports/{created.JobId}/share",
			new HrReportShareExportRequestDto { UserIds = [recipientUserId] },
			JsonOptions);

		shareResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var persisted = await exportRepository.GetAsync(created.JobId);
			persisted.GetSharedUserIds().ShouldNotContain(recipientUserId);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Schedule_Should_Reject_Broad_Owner_To_Narrow_Recipient_Scope()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var recipientUserId = Guid.NewGuid();
		var recipientStageId = await SeedReportStageAsync();
		await SeedReportRecipientLookupUserAsync(
			recipientUserId,
			$"report-schedule-scope-narrow-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await EnsureUserHasStageScopeAsync(recipientUserId, recipientStageId);

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = $"Broad Owner Schedule Guard {marker}",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			RecipientUserIds = [recipientUserId],
			Definition = CreateBasicEmployeeExportRequest("Broad Owner Schedule Guard")
		}, JsonOptions);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Schedule_Run_Should_Recheck_Recipient_Scope()
	{
		var marker = Guid.NewGuid().ToString("N")[..8];
		var ownerUserId = Guid.NewGuid();
		var recipientUserId = Guid.NewGuid();
		var originalStageId = await SeedReportStageAsync();
		var replacementStageId = await SeedReportStageAsync();
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-schedule-owner-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await SeedReportRecipientLookupUserAsync(
			recipientUserId,
			$"report-schedule-recipient-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await EnsureUserHasStageScopeAsync(ownerUserId, originalStageId);
		await EnsureUserHasStageScopeAsync(recipientUserId, originalStageId);

		using var ownerClient = CreateReportClient(
			ownerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Schedule);
		var createResponse = await ownerClient.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = $"Schedule Scope Recheck {marker}",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			RecipientUserIds = [recipientUserId],
			Definition = CreateBasicEmployeeExportRequest("Schedule Scope Recheck")
		}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await createResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();

		await ReplaceUserStageScopeAsync(recipientUserId, replacementStageId);

		var runResponse = await ownerClient.PostAsync($"{BaseUrl}/schedules/{schedule.Id}/run-now", null);
		runResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Schedule_Run_Should_Reject_Owner_Without_Current_Report_View_Permission()
	{
		var marker = Guid.NewGuid().ToString("N")[..8];
		var ownerUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-schedule-revoked-owner-{marker}",
			grantReportView: true);
		using var ownerClient = CreateReportClient(
			ownerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Schedule);
		var createResponse = await ownerClient.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = $"Schedule Owner Permission Recheck {marker}",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			Definition = CreateBasicEmployeeExportRequest("Schedule Owner Permission Recheck")
		}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await createResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();

		await RevokeDirectReportViewPermissionAsync(ownerUserId);

		var runResponse = await ownerClient.PostAsync(
			$"{BaseUrl}/schedules/{schedule.Id}/run-now",
			null);
		runResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Share_Export_Should_Allow_Recipient_Whose_Scope_Covers_Export()
	{
		var marker = Guid.NewGuid().ToString("N")[..8];
		var ownerUserId = Guid.NewGuid();
		var recipientUserId = Guid.NewGuid();
		var sharedStageId = await SeedReportStageAsync();
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-scope-owner-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await SeedReportRecipientLookupUserAsync(
			recipientUserId,
			$"report-scope-recipient-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await EnsureUserHasStageScopeAsync(ownerUserId, sharedStageId);
		await EnsureUserHasStageScopeAsync(recipientUserId, sharedStageId);

		using var ownerClient = CreateReportClient(
			ownerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Export,
			ShikshaMitraSubmodulePermissions.HR.Reports.Share);
		var createResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			CreateBasicEmployeeExportRequest("Compatible Scoped Share"),
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var shareResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports/{created.JobId}/share",
			new HrReportShareExportRequestDto { UserIds = [recipientUserId] },
			JsonOptions);
		shareResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var recipientClient = CreateReportViewClient(recipientUserId);
		var sharedJobResponse = await recipientClient.GetAsync($"{BaseUrl}/exports/{created.JobId}");
		sharedJobResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Share_Export_Should_Reject_Cross_Tenant_Recipient()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var foreignTenantId = Guid.NewGuid();
		var foreignUserId = Guid.NewGuid();
		await SeedCrossTenantReportRecipientAsync(
			foreignTenantId,
			foreignUserId,
			$"report-cross-tenant-{marker}");

		var createResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			CreateBasicEmployeeExportRequest("Cross Tenant Share Guard"),
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var shareResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/exports/{created.JobId}/share",
			new HrReportShareExportRequestDto { UserIds = [foreignUserId] },
			JsonOptions);
		shareResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Shared_Export_Download_Should_Recheck_Recipient_Scope()
	{
		var marker = Guid.NewGuid().ToString("N")[..8];
		var ownerUserId = Guid.NewGuid();
		var recipientUserId = Guid.NewGuid();
		var originalStageId = await SeedReportStageAsync();
		var replacementStageId = await SeedReportStageAsync();
		await SeedReportRecipientLookupUserAsync(
			ownerUserId,
			$"report-download-scope-owner-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await SeedReportRecipientLookupUserAsync(
			recipientUserId,
			$"report-download-scope-recipient-{marker}",
			grantReportView: true,
			grantTenantWideScope: false);
		await EnsureUserHasStageScopeAsync(ownerUserId, originalStageId);
		await EnsureUserHasStageScopeAsync(recipientUserId, originalStageId);

		using var ownerClient = CreateReportClient(
			ownerUserId,
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Export,
			ShikshaMitraSubmodulePermissions.HR.Reports.Share);
		var createResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports",
			CreateBasicEmployeeExportRequest("Download Scope Recheck"),
			JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();
		var shareResponse = await ownerClient.PostAsJsonAsync(
			$"{BaseUrl}/exports/{created.JobId}/share",
			new HrReportShareExportRequestDto { UserIds = [recipientUserId] },
			JsonOptions);
		shareResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		await ReplaceUserStageScopeAsync(recipientUserId, replacementStageId);

		using var recipientClient = CreateReportViewClient(recipientUserId);
		var downloadResponse = await recipientClient.GetAsync($"{BaseUrl}/exports/{created.JobId}/download");
		downloadResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
		var listResponse = await recipientClient.GetAsync($"{BaseUrl}/exports");
		listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var exports = await listResponse.Content.ReadFromJsonAsync<List<HrReportExportJobDto>>(JsonOptions);
		exports.ShouldNotBeNull();
		exports.Select(x => x.Id).ShouldNotContain(created.JobId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Schedules_Should_Reject_Recipients_Without_Report_View_Access()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = "Invalid Recipient Schedule",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InAppAndEmail,
			RecipientUserIds = [Guid.NewGuid()],
			Definition = new HrReportRequestDto
			{
				ReportType = HrReportTypes.EmployeeList,
				DataSource = HrReportDataSources.Employee,
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Share_Export_Should_Reject_Empty_And_Non_View_Recipients()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var blockedUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(blockedUserId, $"report-share-blocked-{marker}", grantReportView: false);

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Filters = new HrReportFilterDto()
		}, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var emptyResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports/{created.JobId}/share", new HrReportShareExportRequestDto
		{
			UserIds = []
		}, JsonOptions);
		emptyResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

		var blockedResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports/{created.JobId}/share", new HrReportShareExportRequestDto
		{
			UserIds = [blockedUserId]
		}, JsonOptions);
		blockedResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Share_Export_Should_Add_Recipients_Without_Removing_Existing_Recipients()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var firstRecipientId = Guid.NewGuid();
		var secondRecipientId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(firstRecipientId, $"report-share-first-{marker}", grantReportView: true);
		await SeedReportRecipientLookupUserAsync(secondRecipientId, $"report-share-second-{marker}", grantReportView: true);

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", CreateBasicEmployeeExportRequest("Additive Share Export"), JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var firstShareResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports/{created.JobId}/share", new HrReportShareExportRequestDto
		{
			UserIds = [firstRecipientId]
		}, JsonOptions);
		firstShareResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var secondShareResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports/{created.JobId}/share", new HrReportShareExportRequestDto
		{
			UserIds = [secondRecipientId]
		}, JsonOptions);
		secondShareResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var sharedJob = await secondShareResponse.Content.ReadFromJsonAsync<HrReportExportJobDto>(JsonOptions);
		sharedJob.ShouldNotBeNull();
		sharedJob.SharedUserIds.ShouldContain(firstRecipientId);
		sharedJob.SharedUserIds.ShouldContain(secondRecipientId);
		sharedJob.SharedUserIds.ShouldNotContain(Guid.Parse(TestAuthHandler.TestUserId));

		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
			using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
			{
				var persisted = await exportRepository.GetAsync(created.JobId);
				persisted.GetSharedUserIds().ShouldContain(firstRecipientId);
				persisted.GetSharedUserIds().ShouldContain(secondRecipientId);
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Share_Export_Should_Reject_When_Only_Owner_Is_Selected()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", CreateBasicEmployeeExportRequest("Owner Only Share Export"), JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var created = await createResponse.Content.ReadFromJsonAsync<HrReportExportCreateResponseDto>(JsonOptions);
		created.ShouldNotBeNull();

		var ownerOnlyResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports/{created.JobId}/share", new HrReportShareExportRequestDto
		{
			UserIds = [Guid.Parse(TestAuthHandler.TestUserId)]
		}, JsonOptions);
		ownerOnlyResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Recipients_Should_Return_Only_Users_With_Report_View_Access()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var allowedUserId = Guid.NewGuid();
		var blockedUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(allowedUserId, $"report-recipient-allowed-{marker}", grantReportView: true);
		await SeedReportRecipientLookupUserAsync(blockedUserId, $"report-recipient-blocked-{marker}", grantReportView: false);

		var response = await Client.GetAsync($"{BaseUrl}/recipients?filter={marker}&maxResultCount=20");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var recipients = await response.Content.ReadFromJsonAsync<List<HrReportRecipientDto>>(JsonOptions);
		recipients.ShouldNotBeNull();
		recipients.Select(x => x.Id).ShouldContain(allowedUserId);
		recipients.Select(x => x.Id).ShouldNotContain(blockedUserId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Recipients_Should_Resolve_Explicit_Ids_Outside_Filter_And_Keep_View_Permission()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var allowedUserId = Guid.NewGuid();
		var blockedUserId = Guid.NewGuid();
		await SeedReportRecipientLookupUserAsync(allowedUserId, $"report-recipient-id-allowed-{marker}", grantReportView: true);
		await SeedReportRecipientLookupUserAsync(blockedUserId, $"report-recipient-id-blocked-{marker}", grantReportView: false);

		var response = await Client.GetAsync($"{BaseUrl}/recipients?filter=no-match-{marker}&maxResultCount=1&ids={allowedUserId},{blockedUserId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var recipients = await response.Content.ReadFromJsonAsync<List<HrReportRecipientDto>>(JsonOptions);
		recipients.ShouldNotBeNull();
		recipients.Select(x => x.Id).ShouldContain(allowedUserId);
		recipients.Select(x => x.Id).ShouldNotContain(blockedUserId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Recipients_Should_Not_Lose_Eligible_Users_After_Ineligible_Search_Matches()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var marker = Guid.NewGuid().ToString("N")[..8];
		var allowedUserId = Guid.NewGuid();

		for (var index = 0; index < 205; index++)
		{
			await SeedReportRecipientLookupUserAsync(
				Guid.NewGuid(),
				$"report-recipient-scale-a{index:000}-{marker}",
				grantReportView: false);
		}

		await SeedReportRecipientLookupUserAsync(
			allowedUserId,
			$"report-recipient-scale-z-{marker}",
			grantReportView: true);

		var response = await Client.GetAsync($"{BaseUrl}/recipients?filter={marker}&maxResultCount=20");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var recipients = await response.Content.ReadFromJsonAsync<List<HrReportRecipientDto>>(JsonOptions);
		recipients.ShouldNotBeNull();
		recipients.Select(x => x.Id).ShouldContain(allowedUserId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Schedule_Permission_Should_Delete_Schedules_But_View_Only_Should_Not()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var scheduleClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.HR.Reports.View,
			ShikshaMitraSubmodulePermissions.HR.Reports.Schedule);

		var scheduleResponse = await scheduleClient.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = "Schedule Permission Delete",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			Definition = new HrReportRequestDto
			{
				ReportType = HrReportTypes.EmployeeList,
				DataSource = HrReportDataSources.Employee,
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);
		scheduleResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await scheduleResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();

		using var viewOnlyClient = CreateClientWithPermissions(ShikshaMitraSubmodulePermissions.HR.Reports.View);
		var forbiddenDelete = await viewOnlyClient.DeleteAsync($"{BaseUrl}/schedules/{schedule.Id}");
		forbiddenDelete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var allowedDelete = await scheduleClient.DeleteAsync($"{BaseUrl}/schedules/{schedule.Id}");
		allowedDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task View_Only_User_Should_Preview_But_Not_Mutate_Reports()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		using var client = CreateClientWithPermissions(ShikshaMitraSubmodulePermissions.HR.Reports.View);

		var previewResponse = await client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Page = 1,
			PageSize = 5,
			Filters = new HrReportFilterDto()
		}, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var schedulesResponse = await client.GetAsync($"{BaseUrl}/schedules");
		schedulesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		var exportResponse = await client.PostAsJsonAsync($"{BaseUrl}/exports", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Filters = new HrReportFilterDto()
		}, JsonOptions);
		exportResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var templateResponse = await client.PostAsJsonAsync($"{BaseUrl}/templates", new HrReportTemplateRequestDto
		{
			Name = "Forbidden Template",
			Category = "Employee",
			DataSource = HrReportDataSources.Employee,
			Visibility = HrReportTemplateVisibility.Private,
			Definition = new HrReportRequestDto
			{
				ReportType = "Forbidden Template",
				DataSource = HrReportDataSources.Employee,
				Columns = ["employeeCode"]
			}
		}, JsonOptions);
		templateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var scheduleResponse = await client.PostAsJsonAsync($"{BaseUrl}/schedules", new HrReportScheduleRequestDto
		{
			Name = "Forbidden Schedule",
			Frequency = HrReportScheduleFrequency.Monthly,
			DeliveryMode = HrReportDeliveryMode.InApp,
			Definition = new HrReportRequestDto
			{
				ReportType = HrReportTypes.EmployeeList,
				DataSource = HrReportDataSources.Employee,
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);
		scheduleResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private async Task EnsureDefaultScheduleOwnerCanRunReportsAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedReportRecipientLookupUserAsync(
			Guid.Parse(TestAuthHandler.TestUserId),
			"api-test-default-report-owner",
			grantReportView: true);
	}

	private async Task SeedReportRecipientLookupUserAsync(
		Guid userId,
		string userName,
		bool grantReportView,
		bool grantTenantWideScope = true)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var userRepository = scope.ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>();
		var permissionGrantRepository = scope.ServiceProvider.GetRequiredService<IRepository<PermissionGrant, Guid>>();
		var userScopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var permissionName = ShikshaMitraSubmodulePermissions.HR.Reports.View;
		var providerKey = userId.ToString("D");

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var existingUsers = await userRepository.GetListAsync(x => x.Id == userId);
			if (existingUsers.Count == 0)
			{
				await userRepository.InsertAsync(
					new IdentityUser(userId, userName, $"{userName}@test.local", tenantId),
					autoSave: false);
			}

			if (grantReportView)
			{
				var existingGrants = await permissionGrantRepository.GetListAsync(x =>
					x.Name == permissionName &&
					x.ProviderName == "U" &&
					x.ProviderKey == providerKey);
				if (existingGrants.Count == 0)
				{
					await permissionGrantRepository.InsertAsync(
						new PermissionGrant(
							Guid.NewGuid(),
							permissionName,
							"U",
							providerKey,
							tenantId),
					autoSave: false);
				}
			}

			if (grantTenantWideScope)
			{
				var existingScopes = await userScopeRepository.GetListAsync(x =>
					x.UserId == userId &&
					x.AcademicYearId == null &&
					x.EducationBoardId == null &&
					x.EducationMediumId == null &&
					x.EducationMediumStageId == null &&
					x.StandardId == null &&
					x.DivisionId == null &&
					x.IsActive);
				if (existingScopes.Count == 0)
				{
					await userScopeRepository.InsertAsync(
						new UserScopeAssignment(
							Guid.NewGuid(),
							tenantId,
							userId: userId,
							description: "API regression test tenant-wide report recipient scope"),
						autoSave: false);
				}
			}

			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedCrossTenantReportRecipientAsync(
		Guid tenantId,
		Guid userId,
		string userName)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var userRepository = scope.ServiceProvider.GetRequiredService<IRepository<IdentityUser, Guid>>();
		var permissionGrantRepository = scope.ServiceProvider.GetRequiredService<IRepository<PermissionGrant, Guid>>();
		var userScopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await userRepository.InsertAsync(
				new IdentityUser(userId, userName, $"{userName}@test.local", tenantId),
				autoSave: false);
			await permissionGrantRepository.InsertAsync(
				new PermissionGrant(
					Guid.NewGuid(),
					ShikshaMitraSubmodulePermissions.HR.Reports.View,
					"U",
					userId.ToString("D"),
					tenantId),
				autoSave: false);
			await userScopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					description: "Cross-tenant report recipient regression fixture"),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task RevokeDirectReportViewPermissionAsync(Guid userId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var permissionGrantRepository = scope.ServiceProvider.GetRequiredService<IRepository<PermissionGrant, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var providerKey = userId.ToString("D");

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var directGrants = await permissionGrantRepository.GetListAsync(x =>
				x.Name == ShikshaMitraSubmodulePermissions.HR.Reports.View &&
				x.ProviderName == "U" &&
				x.ProviderKey == providerKey);
			foreach (var directGrant in directGrants)
			{
				await permissionGrantRepository.DeleteAsync(directGrant, autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private async Task ReplaceUserStageScopeAsync(Guid userId, Guid replacementStageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var userScopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var activeScopes = await userScopeRepository.GetListAsync(x =>
				x.UserId == userId &&
				x.IsActive);
			foreach (var activeScope in activeScopes)
			{
				activeScope.Deactivate();
				await userScopeRepository.UpdateAsync(activeScope, autoSave: false);
			}

			await userScopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					educationMediumStageId: replacementStageId,
					description: "API regression test replacement report stage scope"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedOwnedExportJobsAsync(Guid ownerUserId, int count, string marker)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var filtersJson = JsonSerializer.Serialize(new HrReportFilterDto(), JsonOptions);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			for (var index = 0; index < count; index++)
			{
				var request = new HrReportRequestDto
				{
					ReportType = $"Owned Export {marker} {index:000}",
					DataSource = HrReportDataSources.Employee,
					Format = HrReportFormat.Excel,
					Filters = new HrReportFilterDto()
				};
				await exportRepository.InsertAsync(
					new HrReportExportJob(
						Guid.NewGuid(),
						tenantId,
						ownerUserId,
						request.ReportType,
						HrReportFormat.Excel,
						filtersJson,
						JsonSerializer.Serialize(request, JsonOptions),
						DateTime.UtcNow.AddDays(7)),
					autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> SeedSharedCompletedExportJobAsync(
		Guid ownerUserId,
		Guid recipientUserId,
		string marker,
		DateTime expiresAt,
		DateTime? creationTime = null)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var request = new HrReportRequestDto
		{
			ReportType = $"Expired Shared Export {marker}",
			DataSource = HrReportDataSources.Employee,
			Format = HrReportFormat.Excel,
			Columns = ["employeeCode", "employeeName"],
			Filters = new HrReportFilterDto()
		};
		var filtersJson = JsonSerializer.Serialize(request.Filters, JsonOptions);
		var job = new HrReportExportJob(
			Guid.NewGuid(),
			tenantId,
			ownerUserId,
			request.ReportType,
			HrReportFormat.Excel,
			filtersJson,
			JsonSerializer.Serialize(request, JsonOptions),
			expiresAt);
		job.MarkCompleted($"hr-report-exports/test/{marker}.xlsx", DateTime.UtcNow.AddDays(-2), 1, 128);
		job.ShareWith([recipientUserId]);
		if (creationTime.HasValue)
		{
			job.CreationTime = creationTime.Value;
		}

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await exportRepository.InsertAsync(job, autoSave: false);
			await uow.CompleteAsync();
		}

		return job.Id;
	}

	private async Task SeedFalsePositiveSharedCandidateExportJobsAsync(Guid recipientUserId, int count, string marker)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var filtersJson = JsonSerializer.Serialize(new HrReportFilterDto(), JsonOptions);
		var newestCreationTime = DateTime.UtcNow;

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			for (var index = 0; index < count; index++)
			{
				var request = new HrReportRequestDto
				{
					ReportType = $"False Shared Candidate {marker} {index:000}",
					DataSource = HrReportDataSources.Employee,
					Format = HrReportFormat.Excel,
					Filters = new HrReportFilterDto()
				};
				var job = new HrReportExportJob(
					Guid.NewGuid(),
					tenantId,
					Guid.NewGuid(),
					request.ReportType,
					HrReportFormat.Excel,
					filtersJson,
					JsonSerializer.Serialize(request, JsonOptions),
					DateTime.UtcNow.AddDays(7));
				job.CreationTime = newestCreationTime.AddMinutes(index);
				SetSharedUserIdsJson(job, $"[\"{recipientUserId:D}\", \"not-a-valid-guid\"");
				await exportRepository.InsertAsync(job, autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private async Task SeedUnrelatedExportJobsAsync(int count, string marker)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var filtersJson = JsonSerializer.Serialize(new HrReportFilterDto(), JsonOptions);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			for (var index = 0; index < count; index++)
			{
				var request = new HrReportRequestDto
				{
					ReportType = $"Unrelated Export {marker} {index:000}",
					DataSource = HrReportDataSources.Employee,
					Format = HrReportFormat.Excel,
					Filters = new HrReportFilterDto()
				};
				await exportRepository.InsertAsync(
					new HrReportExportJob(
						Guid.NewGuid(),
						tenantId,
						Guid.NewGuid(),
						request.ReportType,
						HrReportFormat.Excel,
						filtersJson,
						JsonSerializer.Serialize(request, JsonOptions),
						DateTime.UtcNow.AddDays(7)),
					autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> SeedExportJobAsync(
		Guid ownerUserId,
		string status,
		string marker,
		DateTime? expiresAt = null,
		HrReportRequestDto? request = null,
		DateTime? creationTime = null)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		request ??= CreateBasicEmployeeExportRequest($"Seeded Export {marker} {status}");
		var job = new HrReportExportJob(
			Guid.NewGuid(),
			tenantId,
			ownerUserId,
			request.ReportType,
			string.Equals(request.Format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
				? HrReportFormat.Pdf
				: HrReportFormat.Excel,
			JsonSerializer.Serialize(request.Filters ?? new HrReportFilterDto(), JsonOptions),
			JsonSerializer.Serialize(request, JsonOptions),
			expiresAt ?? DateTime.UtcNow.AddDays(7));
		if (creationTime.HasValue)
		{
			job.CreationTime = creationTime.Value;
		}

		switch (status)
		{
			case HrReportExportStatus.Running:
				job.MarkRunning(DateTime.UtcNow.AddMinutes(-5));
				break;
			case HrReportExportStatus.Completed:
				job.MarkCompleted($"hr-report-exports/test/{marker}.xlsx", DateTime.UtcNow.AddMinutes(-4), 1, 128);
				break;
			case HrReportExportStatus.Failed:
				job.MarkFailed("Seeded export failure", DateTime.UtcNow.AddMinutes(-4));
				break;
			case HrReportExportStatus.Cancelled:
				job.MarkCancelled(DateTime.UtcNow.AddMinutes(-4));
				break;
		}

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await exportRepository.InsertAsync(job, autoSave: false);
			await uow.CompleteAsync();
		}

		return job.Id;
	}

	private async Task<Guid> GetExportGenerationIdAsync(Guid jobId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var job = await exportRepository.GetAsync(jobId);
			using var storedRequest = JsonDocument.Parse(job.RequestJson);
			var generationId = storedRequest.RootElement.GetProperty("generationId").GetGuid();
			return generationId != Guid.Empty
				? generationId
				: throw new InvalidOperationException("The report export job has no generation id.");
		}
	}

	private async Task<HrReportExportScopeSnapshot> GetExportScopeSnapshotAsync(Guid jobId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var exportRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var job = await exportRepository.GetAsync(jobId);
			using var storedRequest = JsonDocument.Parse(job.RequestJson);
			var scopeSnapshot = storedRequest.RootElement.GetProperty("scope")
				.Deserialize<HrReportExportScopeSnapshot>(JsonOptions);
			return scopeSnapshot
				?? throw new InvalidOperationException("The report export job has no scope snapshot.");
		}
	}

	private static HrReportRequestDto CreateBasicEmployeeExportRequest(string reportType) => new()
	{
		ReportType = reportType,
		DataSource = HrReportDataSources.Employee,
		Format = HrReportFormat.Excel,
		Columns = ["employeeCode", "employeeName"],
		Filters = new HrReportFilterDto()
	};

	private static void SetSharedUserIdsJson(HrReportExportJob job, string sharedUserIdsJson)
	{
		var property = typeof(HrReportExportJob).GetProperty(nameof(HrReportExportJob.SharedUserIdsJson));
		property.ShouldNotBeNull();
		var setter = property.GetSetMethod(nonPublic: true);
		setter.ShouldNotBeNull();
		setter.Invoke(job, new object?[] { sharedUserIdsJson });
	}

	private static HrReportRequestDto CreateMixedCasePayrollReportRequest() => new()
	{
		ReportType = "Mixed Case Payroll Report",
		DataSource = HrReportDataSources.Payroll,
		Format = HrReportFormat.Excel,
		Columns = ["department", "rowCount", "sum_grossSalary"],
		Groups = ["department"],
		Sorts = [new HrReportSortDto { Field = "sum_grossSalary", Direction = "DESC" }],
		Summaries = [new HrReportSummaryDto { Field = "grossSalary", Function = "SUM" }],
		Chart = new HrReportChartDefinitionDto
		{
			Type = "BAR",
			CategoryField = "department",
			ValueField = "grossSalary",
			Function = "AVG"
		},
		Filters = new HrReportFilterDto
		{
			Conditions =
			[
				new HrReportFilterConditionDto
				{
					Field = "department",
					Operator = "IN",
					Values = ["Academics", "Administration"]
				}
			]
		}
	};

	private static async Task AssertProblemContainsAsync(HttpResponseMessage response, string key, string value)
	{
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain(key);
		body.ShouldContain(value);
	}

	private static HrReportRequestDto CreateInvalidEnumReportRequest() => new()
	{
		ReportType = "Invalid Payroll Enum Report",
		DataSource = HrReportDataSources.Payroll,
		Columns = ["employeeCode", "grossSalary"],
		Sorts = [new HrReportSortDto { Field = "employeeCode", Direction = "sideways" }],
		Summaries = [new HrReportSummaryDto { Field = "grossSalary", Function = "median" }],
		Chart = new HrReportChartDefinitionDto
		{
			Type = HrReportChartType.Bar,
			CategoryField = "department",
			ValueField = "grossSalary",
			Function = "median"
		},
		Filters = new HrReportFilterDto
		{
			Conditions =
			[
				new HrReportFilterConditionDto
				{
					Field = "department",
					Operator = "near",
					Value = "Academics"
				}
			]
		}
	};

	private static HrReportRequestDto CreateInvalidChartTypeReportRequest() => new()
	{
		ReportType = "Invalid Chart Type Report",
		DataSource = HrReportDataSources.Employee,
		Columns = ["employeeCode", "department"],
		Groups = ["department"],
		Chart = new HrReportChartDefinitionDto
		{
			Type = "radar",
			CategoryField = "department",
			Function = HrReportSummaryFunction.Count
		},
		Filters = new HrReportFilterDto()
	};

	private static HrReportRequestDto CreateInvalidChartValueFieldReportRequest() => new()
	{
		ReportType = "Invalid Chart Value Field Report",
		DataSource = HrReportDataSources.Employee,
		Columns = ["employeeCode", "department"],
		Groups = ["department"],
		Chart = new HrReportChartDefinitionDto
		{
			Type = HrReportChartType.Bar,
			CategoryField = "department",
			ValueField = "notAReportField",
			Function = HrReportSummaryFunction.Count
		},
		Filters = new HrReportFilterDto()
	};

	private async Task<PersonalInfoResponse> CreateEmployeeInStageAsync(string firstNameSuffix, Guid stageId)
	{
		var personalInfo = EmployeeTestHelper.BuildPersonalInfoRequest(firstNameSuffix);
		personalInfo.Department = firstNameSuffix;
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync("/api/employees/personal-info", content);
		if (!response.IsSuccessStatusCode)
		{
			var payload = await response.Content.ReadAsStringAsync();
			throw new HttpRequestException(
				$"Employee create failed with {(int) response.StatusCode} {response.StatusCode}: {payload}",
				null,
				response.StatusCode);
		}

		var employee = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create report test employee");
		if (!employee.UserId.HasValue)
		{
			throw new InvalidOperationException("Report test employee was created without a user id");
		}

		await AssignEmployeeStageAcademicPermissionAsync(employee.UserId.Value, stageId);
		return employee;
	}

	private async Task AssignEmployeeStageAcademicPermissionAsync(Guid userId, Guid stageId)
	{
		var scope = await ResolveScopeRowForStageAsync(stageId);
		var response = await Client.PostAsJsonAsync(
			"/api/app/user-scope/bulk-assign",
			new
			{
				userIds = new[] { userId },
				academicYearId = scope.AcademicYearId,
				scopes = new[]
				{
					new
					{
						educationBoardId = scope.BoardId,
						educationMediumId = scope.MediumId,
						educationMediumStageId = scope.StageId
					}
				},
				description = "HTTP API report test employee academic permission"
			},
			JsonOptions);
		if (!response.IsSuccessStatusCode)
		{
			var payload = await response.Content.ReadAsStringAsync();
			throw new HttpRequestException(
				$"Employee report academic permission failed with {(int) response.StatusCode} {response.StatusCode}: {payload}",
				null,
				response.StatusCode);
		}
	}

	private async Task<StageScopeRow> ResolveScopeRowForStageAsync(Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var stageRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		var mediumRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>();
		var boardRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationBoard, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var stage = await stageRepository.GetAsync(stageId);
			var medium = await mediumRepository.GetAsync(stage.EducationMediumId);
			var board = await boardRepository.GetAsync(medium.EducationBoardId);
			await uow.CompleteAsync();
			return new StageScopeRow(board.AcademicYearId, board.Id, medium.Id, stage.Id);
		}
	}

	private sealed record StageScopeRow(Guid AcademicYearId, Guid BoardId, Guid MediumId, Guid StageId);

	private async Task SeedEmployeePolicyAssignmentsAsync(params (Guid EmployeeId, string TargetDisplayName)[] assignments)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var attendancePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var payrollPolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var attendancePolicies = await attendancePolicyRepository.GetListAsync(x => x.Status == HrPolicyStatus.Active);
			var payrollPolicies = await payrollPolicyRepository.GetListAsync(x => x.Status == HrPolicyStatus.Active);
			var policyPair =
				(from attendance in attendancePolicies
				 join payroll in payrollPolicies on attendance.AcademicYearId equals payroll.AcademicYearId
				 select new { Attendance = attendance, Payroll = payroll })
				.First();

			foreach (var assignment in assignments)
			{
				await assignmentRepository.InsertAsync(new HrPolicyAssignment(
					Guid.NewGuid(),
					tenantId,
					policyPair.Attendance.AcademicYearId,
					HrPolicyAssignmentTargetType.Employee,
					assignment.EmployeeId.ToString("D"),
					assignment.TargetDisplayName,
					policyPair.Attendance.Id,
					policyPair.Payroll.Id,
					null,
					DateTime.UtcNow.Date,
					null,
					true,
					assignmentReason: $"Report scope regression {assignment.TargetDisplayName}"), autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private async Task SeedEmployeeBackedReportRowsAsync(
		string dataSource,
		PersonalInfoResponse inScopeEmployee,
		PersonalInfoResponse outOfScopeEmployee,
		Guid inScopeStageId,
		Guid outOfScopeStageId)
	{
		switch (dataSource)
		{
			case HrReportDataSources.Attendance:
				await SeedReportAttendanceAsync(inScopeEmployee.Id, inScopeStageId, "Scoped in-scope report attendance");
				await SeedReportAttendanceAsync(outOfScopeEmployee.Id, outOfScopeStageId, "Scoped out-of-scope report attendance");
				break;
			case HrReportDataSources.Leave:
				var leavePolicy = await GetSeededLeavePolicyAsync();
				await SeedReportLeaveAsync(
					inScopeEmployee.Id,
					leavePolicy.PolicyId,
					leavePolicy.RuleId,
					leavePolicy.PaidType,
					"Scoped in-scope report leave");
				await SeedReportLeaveAsync(
					outOfScopeEmployee.Id,
					leavePolicy.PolicyId,
					leavePolicy.RuleId,
					leavePolicy.PaidType,
					"Scoped out-of-scope report leave");
				break;
			case HrReportDataSources.Payroll:
				await SeedReportPayrollSnapshotAsync(inScopeEmployee, "2026-06", 1111m);
				await SeedReportPayrollSnapshotAsync(outOfScopeEmployee, "2026-06", 2222m);
				break;
			case HrReportDataSources.Performance:
				await SeedReportPerformanceAsync(inScopeEmployee.Id, outOfScopeEmployee.Id);
				break;
		}
	}

	private static HrReportFilterDto BuildScopedReportFilters(
		string dataSource,
		Guid inScopeEmployeeId,
		Guid outOfScopeEmployeeId)
	{
		var filters = dataSource switch
		{
			HrReportDataSources.Attendance => new HrReportFilterDto
			{
				DateField = "attendanceDate",
				FromDate = new DateOnly(2026, 6, 1),
				ToDate = new DateOnly(2026, 6, 30)
			},
			HrReportDataSources.Leave => new HrReportFilterDto
			{
				FromDate = new DateOnly(2026, 6, 1),
				ToDate = new DateOnly(2026, 6, 30)
			},
			HrReportDataSources.Payroll => new HrReportFilterDto { Month = "2026-06" },
			HrReportDataSources.Performance => new HrReportFilterDto
			{
				DateField = "finalizedAt",
				FromDate = new DateOnly(2026, 6, 1),
				ToDate = new DateOnly(2026, 6, 30)
			},
			_ => new HrReportFilterDto()
		};
		filters.EmployeeIds = [inScopeEmployeeId, outOfScopeEmployeeId];
		return filters;
	}

	private async Task SeedReportAttendanceAsync(Guid employeeId, Guid stageId, string remarks)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var attendanceDate = new DateOnly(2026, 6, 12);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await attendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = tenantId,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate,
				EducationMediumStageId = stageId,
				StatusType = EmployeeAttendanceStatusType.Present,
				Source = AttendanceRecordSource.Manual,
				TimeIn = new DateTimeOffset(attendanceDate.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero),
				TimeOut = new DateTimeOffset(attendanceDate.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero),
				Remarks = remarks
			}, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedReportLeaveAsync(
		Guid employeeId,
		Guid leavePolicyId,
		Guid leavePolicyRuleId,
		LeavePaidType paidType,
		string reason)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var leaveRepository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeLeave, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var leaveDate = new DateTime(2026, 6, 13);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var leave = EmployeeLeave.Create(
				Guid.NewGuid(),
				tenantId,
				employeeId,
				leavePolicyId,
				leavePolicyRuleId,
				paidType,
				new DateTimeOffset(leaveDate),
				new DateTimeOffset(leaveDate),
				reason);
			typeof(EmployeeLeave)
				.GetProperty(nameof(EmployeeLeave.PublicId))!
				.SetValue(leave, Math.Abs(Guid.NewGuid().GetHashCode()));
			await leaveRepository.InsertAsync(leave, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<(Guid PolicyId, Guid RuleId, LeavePaidType PaidType)> GetSeededLeavePolicyAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var leavePolicyRepository = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>();
		var leavePolicyRuleRepository = scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicyRule, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var policies = await leavePolicyRepository.GetListAsync();
			if (policies.Count > 0)
			{
				var policyId = policies.First().Id;
				var rules = await leavePolicyRuleRepository.GetListAsync(x => x.LeavePolicyId == policyId && x.IsActive);
				var rule = rules.First(x => x.PaidType == LeavePaidType.Paid);
				return (policyId, rule.Id, rule.PaidType);
			}

			var academicYears = await academicYearRepository.GetListAsync();
			var academicYearId = academicYears.First().Id;
			var policy = new LeavePolicy(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				$"Report Scope Leave Policy {Guid.NewGuid():N}"[..50],
				academicYears.First().StartDate,
				academicYears.First().EndDate,
				HrPolicyStatus.Active);
			var policyRule = new LeavePolicyRule(
				Guid.NewGuid(),
				tenantId,
				policy.Id,
				LeavePaidType.Paid,
				LeaveEntitlementMode.Limited,
				LeaveAccrualPolicyType.Yearly,
				12,
				carryForward: false,
				carryForwardLimit: null,
				encashable: false,
				encashmentTiming: null,
				requiresApproval: true,
				minNoticeDays: 0,
				maxDaysPerRequest: 30,
				allowHalfDay: true,
				attachmentRequiredAfterDays: null,
				allowNegativeBalance: false);
			var unpaidRule = new LeavePolicyRule(
				Guid.NewGuid(),
				tenantId,
				policy.Id,
				LeavePaidType.Unpaid,
				LeaveEntitlementMode.Unlimited,
				LeaveAccrualPolicyType.Yearly,
				null,
				carryForward: false,
				carryForwardLimit: null,
				encashable: false,
				encashmentTiming: null,
				requiresApproval: true,
				minNoticeDays: 0,
				maxDaysPerRequest: 30,
				allowHalfDay: true,
				attachmentRequiredAfterDays: null,
				allowNegativeBalance: true);
			policy.ReplaceRules([policyRule, unpaidRule]);
			await leavePolicyRepository.InsertAsync(policy, autoSave: false);
			await uow.CompleteAsync();
			return (policy.Id, policyRule.Id, policyRule.PaidType);
		}
	}

	private async Task SeedReportPayrollSnapshotAsync(PersonalInfoResponse employee, string monthYear, decimal netPay)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var snapshotRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), tenantId, monthYear, employee.Id)
			{
				EmployeeName = employee.FullName,
				EmployeeIdentifier = employee.EmployeeCode,
				BasicSalary = netPay,
				GrossSalary = netPay,
				FullGrossSalary = netPay,
				NetPay = netPay,
				PayableDays = 30,
				TotalWorkingDays = 30,
				PayableUnits = 30,
				BasisDays = 30,
				ReviewStatus = "PENDING"
			};
			await snapshotRepository.InsertAsync(snapshot, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedReportPerformanceAsync(Guid inScopeEmployeeId, Guid outOfScopeEmployeeId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var cycleRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProfessionalReviewCycle, Guid>>();
		var templateRepository = scope.ServiceProvider.GetRequiredService<IRepository<PerformanceTemplate, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<PerformanceReviewAssignment, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var cycle = new ProfessionalReviewCycle(
				Guid.NewGuid(),
				tenantId,
				"Scoped Report Cycle",
				new DateTime(2026, 6, 1),
				new DateTime(2026, 6, 30),
				ProfessionalReviewCycleStatus.Published);
			var template = new PerformanceTemplate(
				Guid.NewGuid(),
				tenantId,
				"Scoped Report Template",
				PerformanceStaffCategory.Teacher,
				roleName: null);
			await cycleRepository.InsertAsync(cycle, autoSave: false);
			await templateRepository.InsertAsync(template, autoSave: false);

			var inScope = new PerformanceReviewAssignment(Guid.NewGuid(), tenantId, cycle.Id, template.Id, inScopeEmployeeId);
			inScope.FinalizeReview(4.5m, "A", new DateTime(2026, 6, 15));
			var outOfScope = new PerformanceReviewAssignment(Guid.NewGuid(), tenantId, cycle.Id, template.Id, outOfScopeEmployeeId);
			outOfScope.FinalizeReview(2.5m, "C", new DateTime(2026, 6, 15));
			await assignmentRepository.InsertAsync(inScope, autoSave: false);
			await assignmentRepository.InsertAsync(outOfScope, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task<Guid> SeedReportStageAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
			var boardRepository = scope.ServiceProvider.GetRequiredService<IEducationBoardRepository>();
			var mediumRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumRepository>();
			var stageRepository = scope.ServiceProvider.GetRequiredService<IEducationMediumStageRepository>();
			var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
			var boardManager = scope.ServiceProvider.GetRequiredService<EducationBoardManager>();
			var mediumManager = scope.ServiceProvider.GetRequiredService<EducationMediumManager>();
			var stageManager = scope.ServiceProvider.GetRequiredService<EducationMediumStageManager>();
			var suffix = Guid.NewGuid().ToString("N")[..8];

			var academicYear = await academicYearManager.CreateAsync(
				$"ReportScopeYear-{suffix}",
				new DateTime(2037, 4, 1),
				new DateTime(2038, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);
			var board = await boardManager.CreateAsync(academicYear.Id, EducationBoardType.CBSE);
			await boardRepository.InsertAsync(board, autoSave: false);
			var medium = await mediumManager.CreateAsync(board.Id, $"ReportScopeMedium-{suffix}");
			await mediumRepository.InsertAsync(medium, autoSave: false);
			var stage = await stageManager.CreateAsync(medium.Id, $"ReportScopeStage-{suffix}");
			await stageRepository.InsertAsync(stage, autoSave: false);

			await uow.CompleteAsync();
			return stage.Id;
		}
	}

	private async Task EnsureUserHasStageScopeAsync(Guid userId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					tenantId,
					userId: userId,
					educationMediumStageId: stageId,
					description: "API regression test HR report stage scope"),
				autoSave: false);
			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private HttpClient CreateReportViewClient(Guid userId)
	{
		return CreateReportClient(userId, ShikshaMitraSubmodulePermissions.HR.Reports.View);
	}

	private HttpClient CreateReportClient(Guid userId, params string[] permissions)
	{
		var client = CreateClientWithPermissions(permissions);
		client.DefaultRequestHeaders.Remove(TestAuthHandler.TestUserIdHeader);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString("D"));
		return client;
	}

	private HttpClient CreateClientWithPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "HR Viewer");
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}
}
