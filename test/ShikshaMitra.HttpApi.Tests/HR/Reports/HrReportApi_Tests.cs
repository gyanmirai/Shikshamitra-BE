using System;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
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
}
