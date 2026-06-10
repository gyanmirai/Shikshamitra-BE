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
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
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
	public async Task Preview_Should_Warn_When_Advanced_Request_Uses_Unsupported_Source_Capabilities()
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

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.QueryWarnings.ShouldContain(x => x.Contains("not supported for grouping", StringComparison.OrdinalIgnoreCase));
		preview.QueryWarnings.ShouldContain(x => x.Contains("not supported for summaries", StringComparison.OrdinalIgnoreCase));
		preview.QueryWarnings.ShouldContain(x => x.Contains("not supported for charts", StringComparison.OrdinalIgnoreCase));
		preview.QueryWarnings.ShouldContain(x => x.Contains("after source projection", StringComparison.OrdinalIgnoreCase));
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

		using (var scope = Factory.Services.CreateScope())
		{
			var job = scope.ServiceProvider.GetRequiredService<HrReportExportBackgroundJob>();
			await job.ExecuteAsync(new HrReportExportJobArgs
			{
				JobId = createResult.JobId,
				TenantId = Guid.Parse(TestAuthHandler.TestTenantId),
				UserId = Guid.Parse(TestAuthHandler.TestUserId),
				Request = request
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
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Schedules_Should_Create_Pause_Resume_Run_And_Delete()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

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
				Filters = new HrReportFilterDto()
			}
		}, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var schedule = await createResponse.Content.ReadFromJsonAsync<HrReportScheduleDto>(JsonOptions);
		schedule.ShouldNotBeNull();
		schedule.Status.ShouldBe(HrReportScheduleStatus.Active);
		schedule.RecipientUserIds.ShouldNotBeEmpty();

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

		var runResponse = await Client.PostAsync($"{BaseUrl}/schedules/{schedule.Id}/run-now", null);
		runResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var run = await runResponse.Content.ReadFromJsonAsync<HrReportScheduleRunResponseDto>(JsonOptions);
		run.ShouldNotBeNull();
		run.ScheduleId.ShouldBe(schedule.Id);
		run.JobId.ShouldNotBe(default);

		var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/schedules/{schedule.Id}");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Due_Schedules_Should_Queue_Exports_And_Advance_Next_Run()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

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

	private async Task<PersonalInfoResponse> CreateEmployeeInStageAsync(string firstNameSuffix, Guid stageId)
	{
		var personalInfo = EmployeeTestHelper.BuildPersonalInfoRequest(firstNameSuffix);
		personalInfo.OrganizationIds = [stageId];
		personalInfo.Department = firstNameSuffix;
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await Client.PostAsync("/api/employees/personal-info", content);
		if (!response.IsSuccessStatusCode)
		{
			var payload = await response.Content.ReadAsStringAsync();
			throw new HttpRequestException(
				$"Employee create failed with {(int)response.StatusCode} {response.StatusCode}: {payload}",
				null,
				response.StatusCode);
		}

		return await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create report test employee");
	}

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
					leavePolicy.LeaveTypeId,
					"Scoped in-scope report leave");
				await SeedReportLeaveAsync(
					outOfScopeEmployee.Id,
					leavePolicy.PolicyId,
					leavePolicy.RuleId,
					leavePolicy.LeaveTypeId,
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
		Guid leaveTypeId,
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
				leaveTypeId,
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

	private async Task<(Guid PolicyId, Guid RuleId, Guid LeaveTypeId)> GetSeededLeavePolicyAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
		var leaveTypeRepository = scope.ServiceProvider.GetRequiredService<IRepository<LeaveType, Guid>>();
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
				var rule = rules.First();
				return (policyId, rule.Id, rule.LeaveTypeId);
			}

			var academicYears = await academicYearRepository.GetListAsync();
			var academicYearId = academicYears.First().Id;
			var leaveType = new LeaveType(
				Guid.NewGuid(),
				tenantId,
				academicYearId,
				$"Report Scope Leave Type {Guid.NewGuid():N}"[..40],
				"#1A73E8");
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
				leaveType.Id,
				LeaveAccrualPolicyType.Yearly,
				12,
				LeavePaidType.Paid,
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
			policy.ReplaceRules([policyRule]);
			await leaveTypeRepository.InsertAsync(leaveType, autoSave: false);
			await leavePolicyRepository.InsertAsync(policy, autoSave: false);
			await uow.CompleteAsync();
			return (policy.Id, policyRule.Id, leaveType.Id);
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
		var client = CreateClientWithPermissions(ShikshaMitraSubmodulePermissions.HR.Reports.View);
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
