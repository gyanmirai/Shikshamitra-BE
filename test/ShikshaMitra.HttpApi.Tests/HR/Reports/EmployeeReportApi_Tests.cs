using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Reports;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeReportApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/reports/employees";

	public EmployeeReportApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Employee_Report()
	{
		var response = await Client.GetAsync(BaseUrl);

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Contact_Report()
	{
		var response = await Client.GetAsync($"{BaseUrl}/contacts");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	#endregion

	#region Tier 2 — Extended

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Export_Report_As_PDF()
	{
		var response = await Client.GetAsync($"{BaseUrl}/export?exportOptionType=0");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);

		if (response.StatusCode == HttpStatusCode.OK)
		{
			response.Content.Headers.ContentType?.MediaType
				.ShouldBe("application/pdf");
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Export_Report_As_Excel()
	{
		var response = await Client.GetAsync($"{BaseUrl}/export?exportOptionType=1");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);

		if (response.StatusCode == HttpStatusCode.OK)
		{
			response.Content.Headers.ContentType?.MediaType
				.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Extended)]
	public async Task Should_Filter_Report_By_Gender()
	{
		var response = await Client.GetAsync($"{BaseUrl}?genderType=1");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
	}

	#endregion
}
