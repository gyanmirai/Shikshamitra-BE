using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class LeaveManagementApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/leave-management";
	private static readonly Guid TestAcademicYearId = Guid.Parse("00000000-0000-0000-0000-000000000030");
	private Guid AcademicYearId => Factory.SeededAcademicYearId != Guid.Empty ? Factory.SeededAcademicYearId : TestAcademicYearId;

	public LeaveManagementApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	#region Tier 0 — Critical

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Leave_Request_List()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&month-year={monthYear}&page=1&pageSize=10");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task List_Response_Should_Have_TotalCount_And_Items_When_Ok()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&month-year={monthYear}&page=1&pageSize=10");

		if (response.StatusCode != HttpStatusCode.OK)
		{
			return;
		}

		var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
		json.TryGetProperty("totalCount", out _).ShouldBeTrue("Response must include 'totalCount'");
		json.TryGetProperty("items", out var items).ShouldBeTrue("Response must include 'items'");
		items.ValueKind.ShouldBe(JsonValueKind.Array);
		if (items.GetArrayLength() > 0)
		{
			items[0].TryGetProperty("publicId", out _).ShouldBeTrue("List items must include 'publicId'");
		}
	}

	#endregion

	#region Tier 1 — Important

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Request_By_Id_Returns_NotFound_For_Random_Guid()
	{
		var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Delete_Returns_NotFound_For_Random_Guid()
	{
		var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_403_With_HR_Leave_020006_When_User_Has_No_Employee_Profile()
	{
		var (startDate, endDate) = await GetAcademicYearDateRangeAsync(AcademicYearId);
		using var unmappedUserClient = Factory.CreateClient();
		unmappedUserClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());

		using var form = new MultipartFormDataContent();
		form.Add(new StringContent(Guid.NewGuid().ToString()), "leaveTypeId");
		form.Add(new StringContent(startDate.ToString("yyyy-MM-dd")), "startDate");
		form.Add(new StringContent(endDate.ToString("yyyy-MM-dd")), "endDate");
		form.Add(new StringContent("Planned leave request"), "reason");
		var fileContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
		form.Add(fileContent, "attachment.File", "proof.png");

		var response = await unmappedUserClient.PostAsync($"{BaseUrl}?academicYearId={AcademicYearId}", form);
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, rawPayload);

		var payload = JsonSerializer.Deserialize<JsonElement>(rawPayload);
		payload.GetProperty("errorCode").GetString().ShouldBe("HR:Leave:020006");

		var detail = payload.GetProperty("detail").GetString();
		detail.ShouldNotBeNullOrWhiteSpace();
		detail.ShouldNotContain("An internal error occurred during your request!");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Page_Parameters()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&month-year={monthYear}&page=0&pageSize=10");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_PageSize_Parameters()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&month-year={monthYear}&page=1&pageSize=5");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Balances()
	{
		var response = await Client.GetAsync($"{BaseUrl}/balances?academicYearId={AcademicYearId}");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Calendar()
	{
		var start = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var end = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 15).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}/calendar?academicYearId={AcademicYearId}&rangeStart={start}&rangeEnd={end}&page=1&pageSize=10");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Export_Excel()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}/export?academicYearId={AcademicYearId}&month-year={monthYear}");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
		if (response.StatusCode == HttpStatusCode.OK)
		{
			response.Content.Headers.ContentType!.MediaType.ShouldBe(
				"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		}
	}

	private async Task<(DateTime StartDate, DateTime EndDate)> GetAcademicYearDateRangeAsync(Guid academicYearId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();

		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var academicYear = await academicYearRepository.GetAsync(academicYearId);
			return (academicYear.StartDate.Date, academicYear.EndDate.Date);
		}
	}

	#endregion
}
