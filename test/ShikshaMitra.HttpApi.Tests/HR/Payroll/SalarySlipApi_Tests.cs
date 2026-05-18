using System;
using System.Net;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class SalarySlipApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/salary-slips";

	public SalarySlipApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Salary_Slip()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SlipGet");

		var response = await Client.GetAsync(
			$"{BaseUrl}?employeeId={empId}&monthYear=APRIL-2026");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.NotFound,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Salary_Slip_Pdf()
	{
		var empId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "SlipPdf");

		var response = await Client.GetAsync(
			$"{BaseUrl}/pdf?employeeId={empId}&monthYear=APRIL-2026");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.OK,
			HttpStatusCode.NotFound,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);

		if (response.StatusCode == HttpStatusCode.OK)
		{
			response.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
			var bytes = await response.Content.ReadAsByteArrayAsync();
			bytes.Length.ShouldBeGreaterThan(0);
			bytes[0].ShouldBe((byte) '%');
			bytes[1].ShouldBe((byte) 'P');
			bytes[2].ShouldBe((byte) 'D');
			bytes[3].ShouldBe((byte) 'F');
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Missing_Params()
	{
		var response = await Client.GetAsync(BaseUrl);

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Error_For_Invalid_Month_Format()
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}?employeeId={Guid.NewGuid()}&monthYear=invalid");

		response.StatusCode.ShouldBeOneOf(
			HttpStatusCode.BadRequest,
			HttpStatusCode.Forbidden,
			HttpStatusCode.InternalServerError);
	}

	#endregion
}
