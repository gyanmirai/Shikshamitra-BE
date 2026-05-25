using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
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
		var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			Page = 1,
			PageSize = 10,
			Filters = new HrReportFilterDto()
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var preview = await response.Content.ReadFromJsonAsync<HrReportPreviewDto>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.Page.ShouldBe(1);
		preview.PageSize.ShouldBeLessThanOrEqualTo(10);
		preview.Rows.Count.ShouldBeLessThanOrEqualTo(preview.PageSize);
		preview.Columns.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Options_Should_Return_Report_Filter_Metadata()
	{
		var response = await Client.GetAsync($"{BaseUrl}/options");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var options = await response.Content.ReadFromJsonAsync<HrReportOptionsDto>(JsonOptions);
		options.ShouldNotBeNull();
		options.ReportTypes.ShouldContain(x => x.Id == HrReportTypes.EmployeeList);
		options.Statuses.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Export_Should_Create_Queued_Job_And_Return_Job_Status()
	{
		var createResponse = await Client.PostAsJsonAsync($"{BaseUrl}/exports", new HrReportRequestDto
		{
			ReportType = HrReportTypes.EmployeeList,
			Format = HrReportFormat.Excel,
			Filters = new HrReportFilterDto()
		}, JsonOptions);

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
}
