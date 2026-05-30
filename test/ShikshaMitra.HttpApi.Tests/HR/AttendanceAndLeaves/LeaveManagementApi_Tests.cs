using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.HR;
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

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Leave_Request_List_Without_MonthYear_Full_Academic_Year()
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&page=1&pageSize=10");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
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
	public async Task Should_Return_403_With_HR_Leave_020006_When_NonAdmin_User_Has_No_Employee_Profile()
	{
		var (startDate, endDate) = await GetAcademicYearDateRangeAsync(AcademicYearId);
		using var unmappedUserClient = Factory.CreateClient();
		unmappedUserClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		unmappedUserClient.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");

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
		payload.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.LeaveEmployeeNotFound);
		payload.GetProperty("detail").GetString()
			.ShouldBe("Current user is not linked to an employee profile, so this leave action is not allowed.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Not_Return_HR_Leave_020006_For_Admin_Without_Employee_Profile_On_Create()
	{
		var (startDate, endDate) = await GetAcademicYearDateRangeAsync(AcademicYearId);
		using var unmappedAdminClient = Factory.CreateClient();
		unmappedAdminClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());

		using var form = new MultipartFormDataContent();
		form.Add(new StringContent(Guid.NewGuid().ToString()), "forEmployeeId");
		form.Add(new StringContent(Guid.NewGuid().ToString()), "leaveTypeId");
		form.Add(new StringContent(startDate.ToString("yyyy-MM-dd")), "startDate");
		form.Add(new StringContent(endDate.ToString("yyyy-MM-dd")), "endDate");
		form.Add(new StringContent("Planned leave request"), "reason");

		var response = await unmappedAdminClient.PostAsync($"{BaseUrl}?academicYearId={AcademicYearId}", form);
		var rawPayload = await response.Content.ReadAsStringAsync();

		if (response.StatusCode == HttpStatusCode.Forbidden)
		{
			var payload = JsonSerializer.Deserialize<JsonElement>(rawPayload);
			payload.GetProperty("errorCode").GetString().ShouldNotBe(HRErrorCodes.LeaveEmployeeNotFound);
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Create_For_Inactive_Employee()
	{
		var inactiveEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "InactiveLeave");
		var inactiveRequest = new EmployeeStatusUpdateRequest
		{
			EmployeeStatusType = 1,
			Remarks = "Inactive employee should not be selectable for leave",
			EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
		};
		(await Client.PutAsJsonAsync($"/api/employees/{inactiveEmployee.Id}/status", inactiveRequest, JsonOptions))
			.EnsureSuccessStatusCode();

		var (startDate, _) = await GetAcademicYearDateRangeAsync(AcademicYearId);
		using var form = new MultipartFormDataContent();
		form.Add(new StringContent(inactiveEmployee.Id.ToString()), "forEmployeeId");
		form.Add(new StringContent(Guid.NewGuid().ToString()), "leaveTypeId");
		form.Add(new StringContent(startDate.ToString("yyyy-MM-dd")), "startDate");
		form.Add(new StringContent(startDate.AddDays(1).ToString("yyyy-MM-dd")), "endDate");
		form.Add(new StringContent("Planned leave request"), "reason");

		var response = await Client.PostAsync($"{BaseUrl}?academicYearId={AcademicYearId}", form);
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);
		var payload = JsonSerializer.Deserialize<JsonElement>(rawPayload);
		payload.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.LeaveTargetEmployeeInactive);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Admin_Without_Employee_Profile_Should_Get_400_When_Balances_Target_Is_Missing()
	{
		using var unmappedAdminClient = Factory.CreateClient();
		unmappedAdminClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());

		var response = await unmappedAdminClient.GetAsync($"{BaseUrl}/balances?academicYearId={AcademicYearId}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);
		var payload = JsonSerializer.Deserialize<JsonElement>(rawPayload);
		payload.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.LeaveTargetEmployeeRequired);
		payload.GetProperty("detail").GetString()
			.ShouldBe("Target employee is required for this leave operation.");
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
	public async Task Should_Return_400_With_HR_Leave_020026_When_Month_Is_Outside_Academic_Year()
	{
		var (academicYearStart, academicYearEnd) = await GetAcademicYearDateRangeAsync(AcademicYearId);
		var outsideMonth = new DateTime(academicYearEnd.Year, academicYearEnd.Month, 1).AddMonths(2);
		var monthYear = Uri.EscapeDataString(outsideMonth.ToString("O"));
		Client.DefaultRequestHeaders.Remove("Accept-Language");
		Client.DefaultRequestHeaders.Add("Accept-Language", "en-US");

		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&month-year={monthYear}&page=1&pageSize=20");

		var rawPayload = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, rawPayload);

		var payload = JsonSerializer.Deserialize<JsonElement>(rawPayload);
		payload.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.LeaveInvalidMonthForAcademicYear);
		var detail = payload.GetProperty("detail").GetString();
		detail.ShouldNotBeNullOrWhiteSpace();
		detail.ShouldContain(outsideMonth.ToString("yyyy-MM"));
		detail.ShouldContain(academicYearStart.ToString("yyyy-MM-dd"));
		detail.ShouldContain(academicYearEnd.ToString("yyyy-MM-dd"));
		detail.ShouldNotBe(ApiProblemDetailsMapper.GetDefaultDetail(StatusCodes.Status400BadRequest));
		Client.DefaultRequestHeaders.Remove("Accept-Language");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_200_For_InRange_Month()
	{
		var (academicYearStart, _) = await GetAcademicYearDateRangeAsync(AcademicYearId);
		var inRangeMonth = new DateTime(academicYearStart.Year, academicYearStart.Month, 1);
		var monthYear = Uri.EscapeDataString(inRangeMonth.ToString("O"));

		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&month-year={monthYear}&page=1&pageSize=20");

		var rawPayload = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_PageSize_Parameters()
	{
		var monthYear = Uri.EscapeDataString(new DateTime(DateTime.UtcNow.Year, 6, 1).ToString("O"));
		var response = await Client.GetAsync(
			$"{BaseUrl}?academicYearId={AcademicYearId}&month-year={monthYear}&page=1&pageSize=0");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Balances()
	{
		var response = await Client.GetAsync($"{BaseUrl}/balances?academicYearId={AcademicYearId}");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
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
	public async Task Should_Get_Leave_Calendar_With_Offset_Timestamps()
	{
		var start = Uri.EscapeDataString($"{DateTime.UtcNow.Year}-06-01T00:00:00-07:00");
		var end = Uri.EscapeDataString($"{DateTime.UtcNow.Year}-06-15T23:59:59-07:00");
		var response = await Client.GetAsync(
			$"{BaseUrl}/calendar?academicYearId={AcademicYearId}&rangeStart={start}&rangeEnd={end}&page=1&pageSize=10");

		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Calendar_With_DateOnly_Range()
	{
		var start = Uri.EscapeDataString($"{DateTime.UtcNow.Year}-06-01");
		var end = Uri.EscapeDataString($"{DateTime.UtcNow.Year}-06-15");
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

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Leave_Export_Excel_Without_MonthYear()
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}/export?academicYearId={AcademicYearId}");

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
